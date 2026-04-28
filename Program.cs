using LoanAccountDisbursementSvanidhi.BusinessLayer;
using LoanAccountDisbursementSvanidhi.Crypto;
using LoanAccountDisbursementSvanidhi.Entities;
using Newtonsoft.Json;
using NLog;
using PdfSharp.Pdf.Content.Objects;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static LoanAccountDisbursementSvanidhi.Entities.CBSRequestResponse;
using static LoanAccountDisbursementSvanidhi.Entities.GeneralRequest;
using static LoanAccountDisbursementSvanidhi.Entities.SIDBIResponse;
using static LoanAccountDisbursementSvanidhi.Entities.VFirstRequest;
using static System.Net.WebRequestMethods;

namespace LoanAccountDisbursementSvanidhi
{
    internal class Program
    {
        public static Logger _Logger = LogManager.GetLogger("DisburshmentAndOtherServicesLogger");

        static void Main(string[] args)
        {
           // PDFTest obj = new PDFTest();
            //obj.CallIt();
            //PdfPage1Page3Parser.CallIt();
           // PdfPage2Page4Parser.CallIt();
        }
            static void Main1(string[] args)
        {
            try
            {
                Console.WriteLine("###################### Welcome to Call Disbursement Schedular Main Function #######################");
                _Logger.Info("###################### Welcome to Call Main Function #######################");
                //Get All application, where loan Account is Success
                List<EligibleDataForSchedular> ObjEligibleDataForSchedular = DataLayer.DataLayer.GetEligibleCustomerJourney();
                if (ObjEligibleDataForSchedular.Count > 0)
                {
                    _Logger.Info("Eligible customer journey completed and ready for schedular");
                    //_Logger.Info(JsonConvert.SerializeObject(ObjEligibleDataForSchedular));

                    var API_Response = CBSSvcManager.CBSToken();
                    if (API_Response != null)
                    {
                        string API_Token = API_Response.access_token;
                        foreach (var journey in ObjEligibleDataForSchedular)
                        {
                            try
                            {
                                bool isLoanAccount = journey.isLoanAccountOpened;
                                bool isSidbiDisbursement = journey.IsSIDBIDisbursementMark;
                                bool isSRMCreated = journey.IsSRMCreated;
                                bool isAccountDisburshment = journey.IsLoanDisbursed;
                                bool isQRPushCode = journey.IsQRPushCode;

                                bool isSMSSent = journey.IsSMSSent;
                                bool isEmailSent = journey.IsMailSent;

                                if (isLoanAccount == true)
                                {
                                    _Logger.Info("Start, Welcome to schedular journey for PMS Number " + journey.PMSNumber + " and Journey ID " + journey.JourneyID);
                                    Console.WriteLine("Start, Welcome to schedular journey for PMS Number " + journey.PMSNumber + " and Journey ID " + journey.JourneyID);
                                    _Logger.Info("All Flags Status");
                                    _Logger.Info("isLoanAccount: " + isLoanAccount + " ,isSidbiDisbursement: " + isSidbiDisbursement + " ,isSRMCreated: " + isSRMCreated + " ,isAccountDisburshment: " + isAccountDisburshment + " ,isQRPushCode: " + isQRPushCode + " ,isSMSSent: " + isSMSSent + " ,isEmailSent: " + isEmailSent);

                                    if (isSidbiDisbursement == false)// && isSRMCreated == false && isAccountDisburshment == false && isQRPushCode == false && isSMSSent == false && isEmailSent == false)
                                    {
                                        Console.WriteLine("SIDBI Disbursement Mark Start");

                                        string Request = SIDBIRequest(journey);

                                        Console.WriteLine("SIDBI Disbursement Mark Request: " + JsonConvert.SerializeObject(Request));
                                        _Logger.Info("SIDBI Disbursement Mark JSON Request: " + JsonConvert.SerializeObject(Request));

                                        var RequestID = DataLayer.DataLayer.APIRequestResponseLogs(journey.PMSNumber, journey.JourneyID, JsonConvert.SerializeObject(Request), null, null, null, "Insert", "SIDBIDisbursementMark", null);
                                        Console.WriteLine("API Logs Inserted with RequestID: " + RequestID);
                                        _Logger.Info("Sidbi DisbursementAPI Insert Logs Inserted with RequestID: " + RequestID);
                                        var Response = NonCBSLayer.CallSidbiDisbursementStatusAPI(Request);
                                        if (Response != null)
                                        {
                                            _Logger.Info("Output Sidbi Disbursement Status API Response: " + JsonConvert.SerializeObject(Response));
                                            var isUpdated = DataLayer.DataLayer.APIRequestResponseLogs(journey.PMSNumber, journey.JourneyID, null, null, null, JsonConvert.SerializeObject(Response), "Update", "SIDBIDisbursementMark", RequestID);
                                            _Logger.Info("Sidbi Disbursement API Logs Updated with RequestID: " + isUpdated);
                                            if (RequestID == isUpdated)
                                            {
                                                string status = Response.response.applications[0].application_Status.statusCode;
                                                _Logger.Info("SIDBI Disbursement API Response Status: " + Response.response.applications[0].application_Status.statusCode);
                                                //     if (Response.response.applications[0].application_Status.statusCode == "Y")
                                                if (true)
                                                {
                                                    bool isResponse = DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, Response.response.applications[0].application_Status.statusCode, Response.response.applications[0].application_Status.statusCode, "SIDBIStatus");
                                                    Console.WriteLine("Sidbi Disbursement Status API Successfully executed with update status " + isResponse);
                                                    _Logger.Info("Sidbi Disbursement Status API Successfully executed with update status " + isResponse);
                                                    isSidbiDisbursement = true;
                                                }
                                                else
                                                {
                                                    bool isResponse = DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, Response.response.applications[0].application_Status.statusCode, JsonConvert.SerializeObject(Response.response.applications[0].error_Details), "SIDBIStatus");
                                                    Console.WriteLine("System Call Stopped due to SIDBI API Failure Status with update status " + isResponse);
                                                    _Logger.Info("System Call Stopped due to SIDBI API Failure Status with update status " + isResponse);
                                                }
                                            }
                                        }
                                    }

                                    if (isSidbiDisbursement == true && isSRMCreated == false)// && isAccountDisburshment == false && isQRPushCode == false && isSMSSent == false && isEmailSent == false)
                                    {
                                        Console.WriteLine("SRM Creation Start");
                                        _Logger.Info("SRM Creation Start");

                                        var SRMMainRequest = new SRMCreationRequest() { AcctNo = journey.LoanAccountNumber, CollCode = "PERSONAL", CollVal = journey.LoanAmount, Margin = "0", SecurityName = "PERSONAL" };
                                        PlainRequest SRMPlainReq = new PlainRequest() { OperationName = "SRMCreation", RequestData = JsonConvert.SerializeObject(SRMMainRequest) };
                                        _Logger.Info("SRM Creation Plain JSON Request" + JsonConvert.SerializeObject(SRMPlainReq));
                                        EncRequest SRMEncReq = new EncRequest() { EncReqData = SVND_Crypto.Encrypt(JsonConvert.SerializeObject(SRMPlainReq)) };
                                        string SRMEncRequest = JsonConvert.SerializeObject(SRMEncReq);
                                        _Logger.Info("SRM Creation Encrypted Request" + JsonConvert.SerializeObject(SRMEncReq));

                                        var RequestID = DataLayer.DataLayer.APIRequestResponseLogs(journey.PMSNumber, journey.JourneyID, JsonConvert.SerializeObject(SRMPlainReq), JsonConvert.SerializeObject(SRMEncReq), null, null, "Insert", "SRMCreation", null);
                                        _Logger.Info("SRM Creation API Logs Inserted with RequestID: " + RequestID);

                                        var Response = CBSSvcManager.SRMCreation(journey.PMSNumber, journey.JourneyID, RequestID, SRMEncRequest, API_Token);
                                        if (Response != null)
                                        {
                                            ResponseDataObj responseData = JsonConvert.DeserializeObject<ResponseDataObj>(Response.ResponseData);

                                            _Logger.Info("Output SRM Creation API Response: " + JsonConvert.SerializeObject(Response));

                                            if (Response.Status == "Success")
                                            {
                                                bool isResponse = DataLayer.DataLayer.UpdateSRMResponse(journey.PMSNumber, journey.JourneyID, Response.Status, responseData.ColNum);
                                                Console.WriteLine("SRM Creation API Successfully Run");
                                                _Logger.Info("SRM Creation API Successfully Run");
                                                isSRMCreated = true;
                                            }
                                            else
                                            {
                                                bool isResponse = DataLayer.DataLayer.UpdateSRMResponse(journey.PMSNumber, journey.JourneyID, Response.Status, responseData.ColNum);
                                                Console.WriteLine("System Call Stopped due to SRM Creation API Failure Status with update status " + isResponse);
                                                _Logger.Info("System Call Stopped due to SRM Creation API Failure Status with update status " + isResponse);
                                            }
                                        }
                                    }
                                    if (isSidbiDisbursement == true && isSRMCreated == true && isAccountDisburshment == false)// && isQRPushCode == false && isSMSSent == false && isEmailSent == false)
                                    {
                                        Console.WriteLine("Account Disbursement Start");
                                        _Logger.Info("Account Disbursement Start");

                                        //var LoanDisbursementMainRequest = new LoanDisbursementRequest() { CrAcct = journey.AccountNo, LoanAcct = journey.LoanAccountNumber, LoanAmt = journey.LoanAmount, LoanPeriod = journey.LoanTenure, SolId = journey.Verification_AadhaarBankAccount_AccountNo.Substring(0, 6) };
                                        var LoanDisbursementMainRequest = new LoanDisbursementRequest() { CrAcct = journey.AccountNo, LoanAcct = journey.LoanAccountNumber, LoanAmt = journey.LoanAmount, LoanPeriod = journey.LoanTenure, SolId = journey.Solid };
                                        PlainRequest LoanDisbursementPlainReq = new PlainRequest() { OperationName = "AccountDisbursement", RequestData = JsonConvert.SerializeObject(LoanDisbursementMainRequest) };

                                        _Logger.Info("Account Disbursement Plain JSON Request" + JsonConvert.SerializeObject(LoanDisbursementPlainReq));
                                        EncRequest LoanDisbursementEncReq = new EncRequest() { EncReqData = SVND_Crypto.Encrypt(JsonConvert.SerializeObject(LoanDisbursementPlainReq)) };
                                        string LoanDisbursementEncRequest = JsonConvert.SerializeObject(LoanDisbursementEncReq);
                                        _Logger.Info("Account Disbursement Encrypted Request" + JsonConvert.SerializeObject(LoanDisbursementEncRequest));

                                        var RequestID = DataLayer.DataLayer.APIRequestResponseLogs(journey.PMSNumber, journey.JourneyID, JsonConvert.SerializeObject(LoanDisbursementPlainReq), JsonConvert.SerializeObject(LoanDisbursementEncRequest), null, null, "Insert", "LoanDisbursement", null);
                                        _Logger.Info("Account Disbursement API Logs Inserted with RequestID: " + RequestID);

                                        var LoanDisbursementResponse = CBSSvcManager.LoanDisbursement(journey.PMSNumber, journey.JourneyID, RequestID, LoanDisbursementEncRequest, API_Token);
                                        if (LoanDisbursementResponse.Status == "Success")
                                        {
                                            ResponseDataObj responseData = JsonConvert.DeserializeObject<ResponseDataObj>(LoanDisbursementResponse.ResponseData);
                                            _Logger.Info("Output Loan Disbursement API Response: " + JsonConvert.SerializeObject(LoanDisbursementResponse));
                                            DataLayer.DataLayer.UpdateDisbursementResponse(journey.PMSNumber, journey.JourneyID, LoanDisbursementResponse.Status, responseData.Remarks);
                                            isAccountDisburshment = true;
                                            _Logger.Info("Account Disbursement API Successfully Run");
                                        }
                                        else if (LoanDisbursementResponse.Status == "Failure" && LoanDisbursementResponse.ResponseData == "Error in ESVN AccountDisbursement: FINAL DISBURSMENT ALREADY OVER")
                                        {
                                            _Logger.Info("Output Loan Disbursement API Response: " + JsonConvert.SerializeObject(LoanDisbursementResponse));
                                            DataLayer.DataLayer.UpdateDisbursementResponse(journey.PMSNumber, journey.JourneyID, "Success", LoanDisbursementResponse.ResponseData);
                                            isAccountDisburshment = true;
                                            _Logger.Info("Account Disbursement API Successfully Run");
                                        }
                                        else
                                        {
                                            _Logger.Info("Output Loan Disbursement API Response: " + JsonConvert.SerializeObject(LoanDisbursementResponse));
                                            DataLayer.DataLayer.UpdateDisbursementResponse(journey.PMSNumber, journey.JourneyID, "Others", "Loan Disbursement not given Success/Failure response");
                                            isAccountDisburshment = false;
                                        }
                                    }
                                    if (isSidbiDisbursement == true && isSRMCreated == true && isAccountDisburshment == true && isQRPushCode == false)// && isSMSSent == false && isEmailSent == false)
                                    //if (true)
                                    {
                                        Console.WriteLine("QR Push Code Start");
                                        _Logger.Info("QR Push Code Start");
                                        var QRPushCodeMainRequest = new QRPushCodeRequest() { AccountNumber = journey.AccountNo, MerchantName = journey.CustomerName, Mobile = journey.MobileNo, DOB = journey.Date_of_Birth, Address1 = journey.CommAddress_Address1, Address2 = "", Address3 = "", City = journey.CommAddress_City, State = journey.CommAddress_State, PIN = journey.CommAddress_PINCode, SolId = journey.Solid, EmailId = "vishram.meena@pnb.co.in" };//journey.Solid+"@pnb.co.in"
                                        PlainRequest QRPushCodePlainReq = new PlainRequest() { OperationName = "QRDataPush", RequestData = JsonConvert.SerializeObject(QRPushCodeMainRequest) };

                                        _Logger.Info("QR Push Code Plain JSON Request" + JsonConvert.SerializeObject(QRPushCodePlainReq));

                                        EncRequest QRPushCodeEncReq = new EncRequest() { EncReqData = SVND_Crypto.Encrypt(JsonConvert.SerializeObject(QRPushCodePlainReq)) };
                                        string QRPushCodeEncRequest = JsonConvert.SerializeObject(QRPushCodeEncReq);
                                        _Logger.Info("QR Push Code Encrypted Request" + JsonConvert.SerializeObject(QRPushCodeEncRequest));

                                        var RequestID = DataLayer.DataLayer.APIRequestResponseLogs(journey.PMSNumber, journey.JourneyID, JsonConvert.SerializeObject(QRPushCodePlainReq), JsonConvert.SerializeObject(QRPushCodeEncRequest), null, null, "Insert", "QRDataPush", null);
                                        _Logger.Info("QR Push Code API Logs Inserted with RequestID: " + RequestID);
                                        var QRPushCodeResponse = CBSSvcManager.QRDataPush(journey.PMSNumber, journey.JourneyID, RequestID, QRPushCodeEncRequest, API_Token);
                                        if (QRPushCodeResponse.Status == "Success")
                                        {
                                            ResponseDataObj responseData = JsonConvert.DeserializeObject<ResponseDataObj>(QRPushCodeResponse.ResponseData);
                                            _Logger.Info("Output QR Push Code API Response: " + JsonConvert.SerializeObject(QRPushCodeResponse));
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, QRPushCodeResponse.Status, responseData.Message, "QRPushCode");
                                            isQRPushCode = true;
                                            _Logger.Info("QR Push Code API Successfully Run");
                                        }
                                        else
                                        {
                                            ResponseDataObj responseData = JsonConvert.DeserializeObject<ResponseDataObj>(QRPushCodeResponse.ResponseData);
                                            Console.WriteLine("Output QR Push Code API Response: " + JsonConvert.SerializeObject(QRPushCodeResponse));
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, QRPushCodeResponse.Status, responseData.Message, "QRPushCode");
                                        }
                                    }
                                    if (isSidbiDisbursement == true && isSRMCreated == true && isLoanAccount == true && isQRPushCode == true && isSMSSent == false)//&& isEmailSent == false)
                                    //if (true)
                                    {
                                        Console.WriteLine("SMS Sent Start");
                                        _Logger.Info("SMS Sent Start");
                                        string message = "Dear Customer! Your e- PM SVANidhi Loan amount is disbursed in your operative a/c ending with " + journey.AccountNo.Substring(journey.AccountNo.Length - 4) + " & digitally signed documents has been sent over e-mail. You may also collect the same through branch.PNB";
                                        string respSMS = NonCBSLayer.SendOTPVfirst(journey.PMSNumber, journey.JourneyID, journey.MobileNo, message);
                                        if (respSMS == "Success")
                                        {
                                            //Update SMSSent Status in Journey Table
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, respSMS, "Message successfully sent", "SMSSent");
                                            _Logger.Info("SMS Successfully sent to user");
                                            Console.WriteLine("SMS Successfully sent to user");
                                            isSMSSent = true;
                                        }
                                        else
                                        {
                                            //Update SMSSent Status in Journey Table
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, respSMS, "Error to send sms", "SMSSent");
                                            _Logger.Info("SMS Successfully sent to user");
                                        }
                                    }
                                    if (isSidbiDisbursement == true && isSRMCreated == true && isLoanAccount == true && isQRPushCode == true && isSMSSent == true && isEmailSent == false)
                                    //if (true)
                                    {
                                        Console.WriteLine("Email Sent Start");
                                        _Logger.Info("Email Sent Start");
                                        string OTPFormat = "Dear Customer,<br><br>Congratulation! You have successfully availed the credit facility under e-PM SVANidhi. The attachment contains the Loan agreement & Signed Documents.<br><br>Best regards,<br>";

                                        OTPFormat += "<br><br><br><br>DISCLAIMER<br>The Information transmitted in this email is solely for the addressee. It is confidential and may be legally privileged. Access to this email by anyone else is unauthorized. Any disclosure, copying, distribution or any action taken by anyone other than by the intended recipient is prohibited and may be unlawful. If you are not the intended recipient, then kindly delete the mail from your system. Any opinion or views expressed in this mail may not necessarily reflect that of Punjab National Bank. The bank considers unencrypted email as insecure mode of communication.";

                                        string toEmail = string.IsNullOrEmpty(journey.Email_ID) == true ? "noreply@pnb.bank.in" : journey.Email_ID;
                                        string ccEmail = string.IsNullOrEmpty(journey.BranchEmail) == true ? "noreply@pnb.bank.in" : journey.BranchEmail;
                                        string respEmail = NonCBSLayer.SendEmailForSanctionLetter(journey.PMSNumber, journey.JourneyID, journey.BankSignedDoc, toEmail, OTPFormat, ccEmail);
                                        if (respEmail == "Success")
                                        {
                                            //Update email Status in Database
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, respEmail, "Email successfully sent", "EmailSent");
                                            _Logger.Info("Email Successfully sent to user");
                                            Console.WriteLine("Email Successfully sent to user");
                                            isEmailSent = true;
                                        }
                                        else
                                        {
                                            //Update email Status in Database
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, respEmail, "error to send mail", "EmailSent");
                                            _Logger.Info("Error:Email sending error");
                                        }
                                    }
                                    //document Sent Via SMS
                                    if (isSidbiDisbursement == true && isSRMCreated == true && isLoanAccount == true && isQRPushCode == true && isSMSSent == true && isEmailSent == true)
                                    {
                                        Console.WriteLine("Document Via SMS Sent Start");
                                        _Logger.Info("Document Via SMS Sent Start");

                                        // Assuming 'GetDocumentDownloadLink' generates a link to the document
                                        string documentDownloadLink = GetDocumentDownloadLink(journey.PMSNumber, journey.JourneyID, journey.BankSignedDoc);

                                        // Updated SMS message to include the document download link
                                        string message = "Dear Customer, " + "Your e-PM SVANidhi Loan amount has been disbursed in your account ending with " + journey.AccountNo.Substring(journey.AccountNo.Length - 4) + ". " +
                                                         "You can download digitally signed documents securely using the link below: " + documentDownloadLink + "." +
                                                         " For any assistance, please visit your nearest PNB branch.";

                                        // Sending the SMS
                                        string respSMS = NonCBSLayer.SendDocumentVfirst(journey.PMSNumber, journey.JourneyID, journey.MobileNo, message);
                                        if (respSMS == "Success")
                                        {
                                            // Update SMSSent Status in Journey Table
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, respSMS, "Document sent via Message successfully ", "SMSSent");
                                            _Logger.Info("Document sent via SMS Successfully sent to user");
                                            Console.WriteLine("Document sent via SMS Successfully sent to user");
                                            isSMSSent = true;
                                        }
                                        else
                                        {
                                            // Update SMSSent Status in Journey Table
                                            DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, respSMS, "Error to send Document via sms", "SMSSent");
                                            _Logger.Info("Error sending SMS to user");
                                        }
                                    }
                                    if (isSidbiDisbursement == true && isSRMCreated == true && isLoanAccount == true && isQRPushCode == true && isSMSSent == true && isEmailSent == true)
                                    {
                                        Console.WriteLine("Schedular Journey Completed Start");
                                        DataLayer.DataLayer.UpdateGeneralResponse(journey.PMSNumber, journey.JourneyID, "Success", "Schedular Journey successfully done", "SchedularJourney");
                                        _Logger.Info("Schedular Journey Completed");
                                    }

                                    _Logger.Info("End, Welcome to schedular journey for PMS Number " + journey.PMSNumber + " and Journey ID " + journey.JourneyID);
                                }
                            }
                            catch (Exception ex)
                            {
                                _Logger.Info("Main Program Exception: " + ex.ToString());
                                _Logger.Info("Main Program StackTrace: " + ex.StackTrace);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("CBS Token API Null Response");
                    }
                }
                else
                {
                    Console.WriteLine("No pending record found for Schedular Journey");
                    _Logger.Info("No pending record found for Schedular Journey");
                }
            }
            catch (Exception E_Main)
            {
                _Logger.Info($"\nException Raised at main Block at {E_Main}\n");
            }
            //Console.ReadLine();
        }

        public static string SIDBIRequest(EligibleDataForSchedular JourneyData)
        {

            GeneralRequest.Header header = new GeneralRequest.Header
            {
                apiVersion = ConfigurationManager.AppSettings["apiVersion"],
                clientID = ConfigurationManager.AppSettings["clientID"],
                timeStamp = GetEpochTimestamp()
            };

            Request request = new Request
            {
                applications = new[]
                {
                            new GeneralRequest.Application
                            {
                                application_No = JourneyData.JourneyID,
                                portal_RefNo = JourneyData.PMSNumber,
                                loan_Account_No =JourneyData.LoanAccountNumber,
                                bankName = "PUNJAB NATIONAL BANK",
                                branchName = RemoveSpecialCharacters(JourneyData.BRANCH),
                                ifsc = JourneyData.IFSC,
                                accountNo = JourneyData.AccountNo,
                                paymentAggregatorCode = ConfigurationManager.AppSettings["paymentAggregatorCode"],
                                upiid = (!string.IsNullOrEmpty(JourneyData.UPIID)) ? JourneyData.UPIID : JourneyData.MobileNo + "@upi",
                                dP1QRCode = ConfigurationManager.AppSettings["dP1QRCode"],
                                sanctionDate = JourneyData.JourneySanctionedDate.Value.ToString("dd/MM/yyyy"),
                                sanctionAmount = JourneyData.LoanAmount,
                                disbursementDate =  DateTime.Now.ToString("dd/MM/yyyy"),
                                disbursementAmt = JourneyData.LoanAmount,
                                loan_Tenure=JourneyData.LoanTenure,
                                interestRate =JourneyData.ROI_Disbursement,//"10.25", //ROI
                                IsDisbursedRequest = "Y",
                                SanctioningBranchIFSC = JourneyData.IFSC
                            }
                            },
                rejectionReasonTypeCode = "Disbursed",
                otherReason = "Test Reason"
            };


            // Combining header and request into a single object
            var fullRequest = new
            {
                header = header,
                request = request
            };

            // Serializing the request to JSON
            string requestJson = JsonConvert.SerializeObject(fullRequest);

            // Return the JSON request string
            return requestJson;

        }
        private static long GetEpochTimestamp()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        }
        public static string RemoveSpecialCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Replace CR/LF with space first
            input = input.Replace("\r", " ").Replace("\n", " ");

            // Allow only letters, digits, and normal spaces
            return Regex.Replace(input, @"[^a-zA-Z0-9 ]", "");
        }

        /// <summary>
        /// Generates a secure one-time download link for the loan PDF via SMS.
        ///
        /// URL format  : https://svanidhi.pnb.bank.in/d/{9-char-token}  ← exactly 40 chars
        /// IIS rewrites: /d/{token}  →  /smsdocs/DownloadDoc.ashx?token={token}
        ///
        /// - Token is 9 Base62 chars (~57 billion combos) — cryptographically random
        /// - PDF saved to DocumentStoragePath as {token}.pdf
        /// - DB record in SVND_SMS_DOCUMENT_LINKS: 30-day expiry, max 3 clicks
        /// </summary>
        public static string GetDocumentDownloadLink(string pmsNumber, string journeyId, byte[] docBytes)
        {
            try
            {
                string baseUrl     = ConfigurationManager.AppSettings["DocumentDownloadBaseUrl"];
                string storagePath = ConfigurationManager.AppSettings["DocumentStoragePath"];

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(storagePath))
                {
                    _Logger.Info("GetDocumentDownloadLink: DocumentDownloadBaseUrl or DocumentStoragePath not configured.");
                    return string.Empty;
                }

                // Create storage folder if it doesn't exist (e.g. D:\SMSDocStore\)
                if (!System.IO.Directory.Exists(storagePath))
                    System.IO.Directory.CreateDirectory(storagePath);

                // 9-char Base62 token — cryptographically random, ~13 trillion combinations.
                // We check uniqueness against the DB and retry up to 5 times to guarantee
                // no collision ever results in a duplicate SMS link being sent.
                const int maxRetries = 5;
                string token = null;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    string candidate = GenerateShortToken(9);
                    if (DataLayer.DataLayer.IsTokenUnique(candidate))
                    {
                        token = candidate;
                        _Logger.Info($"GetDocumentDownloadLink: Unique token found on attempt {attempt}: {token}");
                        break;
                    }
                    _Logger.Info($"GetDocumentDownloadLink: Token collision on attempt {attempt}, retrying...");
                }

                if (token == null)
                {
                    _Logger.Info("GetDocumentDownloadLink: Failed to generate a unique token after " + maxRetries + " attempts.");
                    return string.Empty;
                }
                string docFileName = token + ".pdf";
                string docFilePath = System.IO.Path.Combine(storagePath, docFileName);

                // Save PDF bytes to disk — filename is the token, not PMSNumber
                System.IO.File.WriteAllBytes(docFilePath, docBytes);
                _Logger.Info("GetDocumentDownloadLink: Saved to " + docFilePath);

                // Store in DB: expires in 30 days, max 3 clicks
                DateTime expiryDate = DateTime.Now.AddDays(30);
                bool inserted = DataLayer.DataLayer.InsertDocumentDownloadLink(
                    pmsNumber, journeyId, token, docFilePath, expiryDate, maxClicks: 3);

                if (!inserted)
                {
                    _Logger.Info("GetDocumentDownloadLink: DB insert failed for PMSNumber " + pmsNumber);
                    return string.Empty;
                }

                // Final SMS URL — IIS rewrites /d?{token} to DownloadDoc.ashx
                // Total length: 31 (base) + 9 (token) = 40 chars — within ValueFirst's 40-char limit
                string downloadUrl = baseUrl.TrimEnd('/') + "?" + token;
                _Logger.Info("GetDocumentDownloadLink: URL = " + downloadUrl + " (length=" + downloadUrl.Length + ")");

                return downloadUrl;
            }
            catch (Exception ex)
            {
                _Logger.Info("GetDocumentDownloadLink Exception: " + ex.ToString());
                return string.Empty;
            }
        }

        /// <summary>
        /// Generates a cryptographically random token of <paramref name="length"/> Base62 characters.
        /// Alphabet: 0-9, A-Z, a-z  (62 chars).  9 chars => 62^9 ≈ 13 trillion combinations.
        /// </summary>
        private static string GenerateShortToken(int length)
        {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var result = new System.Text.StringBuilder(length);
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                byte[] buffer = new byte[length * 2]; // extra bytes to avoid modulo bias
                rng.GetBytes(buffer);
                for (int i = 0; i < length; i++)
                {
                    // Use two bytes per character to reduce modulo bias
                    int idx = ((buffer[i * 2] << 8) | buffer[i * 2 + 1]) % alphabet.Length;
                    result.Append(alphabet[idx]);
                }
            }
            return result.ToString();
        }


    }
}
