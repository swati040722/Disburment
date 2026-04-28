<%@ WebHandler Language="C#" Class="DownloadDoc" %>

using System;
using System.IO;
using System.Web;
using System.Data.SqlClient;
using System.Configuration;

/// <summary>
/// DownloadDoc.ashx
/// Place this file under /smsdocs/ in the svanidhi.pnb.bank.in web application.
///
/// Reached via the short SMS URL (IIS URL Rewrite forwards here):
///   SMS URL : https://svanidhi.pnb.bank.in/d/{token}   ← 40 chars, within ValueFirst limit
///   Rewritten: /smsdocs/DownloadDoc.ashx?token={token}
///
/// Token is a 9-char Base62 string (cryptographically random, ~13 trillion combos).
///
/// Logic:
///   1. Read token from ?token= query string (set by URL Rewrite rule)
///      Fallback: read from PathInfo (e.g. /smsdocs/DownloadDoc.ashx/TOKEN)
///   2. Query SVND_SMS_DOCUMENT_LINKS — check expiry and click count atomically
///   3. If valid  → increment click count, read PDF file, stream to browser
///   4. If invalid → return 403 with a plain-text explanation
/// </summary>
public class DownloadDoc : IHttpHandler
{
    static readonly string ConnectionString =
        ConfigurationManager.ConnectionStrings["PMSvanidhiContext"].ConnectionString;

    public void ProcessRequest(HttpContext context)
    {
        // Primary: token from ?token= (set by IIS URL Rewrite rule)
        string token = context.Request.QueryString["token"];

        // Fallback 1: token directly in query string (e.g. /d?A3kZ9mQ1r)
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(context.Request.Url.Query))
            token = context.Request.Url.Query.TrimStart('?');

        // Fallback 2: token from PathInfo (e.g. /smsdocs/DownloadDoc.ashx/A3kZ9mQ1r)
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(context.Request.PathInfo))
            token = context.Request.PathInfo.TrimStart('/');

        // --- Guard: token must be present ---
        if (string.IsNullOrWhiteSpace(token))
        {
            // SMS Provider Requirement: return 200 OK without redirection on the static URL
            context.Response.Clear();
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html";
            context.Response.Write($@"
                <html><body style='font-family:Arial;text-align:center;margin-top:80px;'>
                <h2>Punjab National Bank</h2>
                <p>Welcome to the PNB Document Portal.</p>
                <p>Please use the full link provided in your SMS to download your document.</p>
                <p>For assistance call PNB helpline: <strong>1800-180-2222</strong></p>
                </body></html>");
            context.Response.End();
            return;
        }

        // --- Validate token in DB & atomically increment click count ---
        string filePath = ValidateAndIncrementDownloadLink(token);

        if (string.IsNullOrEmpty(filePath))
        {
            // Token not found, expired (>30 days), or click limit (3) reached
            SendError(context, 403,
                "This download link is no longer valid. " +
                "The link may have expired (valid for 30 days) or " +
                "the maximum number of downloads (3) has been reached. " +
                "Please visit your nearest PNB branch to collect a copy.");
            return;
        }

        // --- Guard: file must exist on disk ---
        if (!File.Exists(filePath))
        {
            SendError(context, 404, "Document not found. Please contact your branch.");
            return;
        }

        // --- Stream PDF to browser ---
        byte[] pdfBytes = File.ReadAllBytes(filePath);
        context.Response.Clear();
        context.Response.ContentType = "application/pdf";
        context.Response.AddHeader("Content-Disposition", "attachment; filename=\"LoanDocument.pdf\"");
        context.Response.AddHeader("Content-Length", pdfBytes.Length.ToString());
        context.Response.BinaryWrite(pdfBytes);
        context.Response.Flush();
        context.Response.End();
    }

    /// <summary>
    /// Checks token validity (expiry + click count) and increments click count atomically.
    /// Returns file path if valid, empty string if not.
    /// </summary>
    private string ValidateAndIncrementDownloadLink(string token)
    {
        string filePath = string.Empty;
        try
        {
            // Single atomic UPDATE + OUTPUT — increment only if all conditions pass
            string query = @"
                UPDATE [dbo].[SVND_SMS_DOCUMENT_LINKS]
                SET    ClickCount = ClickCount + 1
                OUTPUT INSERTED.FilePath
                WHERE  Token      = @Token
                  AND  IsActive   = 1
                  AND  ExpiryDate >= GETDATE()
                  AND  ClickCount  < MaxClicks";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Token", token);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                        filePath = reader["FilePath"].ToString();
                }
            }
        }
        catch (Exception ex)
        {
            // Log to Windows Event Log or your preferred logger
            System.Diagnostics.EventLog.WriteEntry("DownloadDoc", ex.ToString(),
                System.Diagnostics.EventLogEntryType.Error);
        }
        return filePath;
    }

    private void SendError(HttpContext context, int statusCode, string message)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/html";
        context.Response.Write($@"
            <html><body style='font-family:Arial;text-align:center;margin-top:80px;'>
            <h2>Punjab National Bank</h2>
            <p style='color:#c00;'>{HttpUtility.HtmlEncode(message)}</p>
            <p>For assistance call PNB helpline: <strong>1800-180-2222</strong></p>
            </body></html>");
        context.Response.End();
    }

    public bool IsReusable => false;
}
