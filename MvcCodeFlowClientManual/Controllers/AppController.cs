
using Intuit.Ipp.OAuth2PlatformClient;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.QueryFilter;
using Intuit.Ipp.Security;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.Net;
using System.Net.Http.Headers;
using Intuit.Ipp.DataService;

namespace MvcCodeFlowClientManual.Controllers
{
    

    [Authorize]
    public class AppController : Controller
    {
        public static string mod;
        public static string expo;

        public static string clientid = ConfigurationManager.AppSettings["clientid"];
        public static string clientsecret = ConfigurationManager.AppSettings["clientsecret"];
        public static string redirectUrl = ConfigurationManager.AppSettings["redirectUrl"];
        public static string stateCSRFToken = "";

        public static string authorizeUrl = "";
        public static string tokenEndpoint = "";
        public static string revocationEndpoint = "";
        public static string userinfoEndpoint = "";
        public static string issuerEndpoint = "";
        public static string code = "";

        public static string access_token = "";
        public static string refresh_token = "";
        public static string identity_token = "";
        public static IList<JsonWebKey> keys;


        


        public ActionResult Index()
        {
            return View();
        }

       
        /// <summary>
        /// Make a test QBO api call with .Net sdk
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> CallService()
        {
            //var principal = User as ClaimsPrincipal;

            ////Make QBO api all without .Net SDK
            //string query = "select * from CompanyInfo";
            //// build the  request
            //string encodedQuery = WebUtility.UrlEncode(query);
            //if (Session["realmId"] != null)
            //{
            //    string realmId = Session["realmId"].ToString();

            //    string qboBaseUrl = ConfigurationManager.AppSettings["QBOBaseUrl"];

            //    //add qbobase url and query
            //    string uri = string.Format("{0}/v3/company/{1}/query?query={2}", qboBaseUrl, realmId, encodedQuery);
               
            //    string result="";
                
            //    try
            //    {
            //        var client = new HttpClient();
                    
            //        client.DefaultRequestHeaders.Add("Accept", "application/json;charset=UTF-8");
            //        client.DefaultRequestHeaders.Add("ContentType", "application/json;charset=UTF-8");
            //        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + principal.FindFirst("access_token").Value);
                    

            //        result = await client.GetStringAsync(uri);
            //        return View("CallService",(object)( "QBO API call success! " + result));
            //    }
            //    catch (Exception ex)
            //    {
            //        return View("CallService",(object)"QBO API call Failed!");
            //    }

       
                //Make QBO api calls using .Net SDK
                if (Session["realmId"] != null)
                {
                    string realmId = Session["realmId"].ToString();

                

                    try
                    {
                    // Use access token to retrieve company Info and create an Invoice
                    //Initialize OAuth2RequestValidator and ServiceContext

                    ServiceContext serviceContext = IntializeContext(realmId);
                    QueryService<CompanyInfo> querySvc = new QueryService<CompanyInfo>(serviceContext);//CompanyInfo call
                    CompanyInfo companyInfo = querySvc.ExecuteIdsQuery("SELECT * FROM CompanyInfo").FirstOrDefault();
                    CreateInvoice(realmId);//US company invoice create
                    return View("CallService", (object)("QBO API calls success! "));
                    }
                    catch (Exception ex)
                    {
                        return View("CallService", (object)"QBO API calls Failed!");
                    }

                }
            else
                return View("CallService",(object)"QBO API call Failed!");
        }

        /// <summary>
        /// Refresh the token by making the call to 
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> RefreshToken()
        {
            //Refresh Token call to tokenendpoint
            var tokenClient = new TokenClient(AppController.tokenEndpoint, AppController.clientid, AppController.clientsecret);
            var principal = User as ClaimsPrincipal;
            var refreshToken = principal.FindFirst("refresh_token").Value;

            TokenResponse response = await tokenClient.RequestRefreshTokenAsync(refreshToken);
            UpdateCookie(response);

            return RedirectToAction("Index");
        }

        /// <summary>
        /// This API creates an Invoice
        /// </summary>
        private void CreateInvoice(string realmId)
        {
          



                // Step 1: Initialize OAuth2RequestValidator and ServiceContext
                ServiceContext serviceContext = IntializeContext(realmId);

            // Step 2: Initialize an Invoice object
            Invoice invoice = new Invoice();
            invoice.Deposit = new Decimal(0.00);
            invoice.DepositSpecified = true;

            // Step 3: Invoice is always created for a customer so lets retrieve reference to a customer and set it in Invoice
            QueryService<Customer> querySvc = new QueryService<Customer>(serviceContext);
            Customer customer = querySvc.ExecuteIdsQuery("SELECT * FROM Customer WHERE CompanyName like 'Amy%'").FirstOrDefault();
            invoice.CustomerRef = new ReferenceType()
            {
                Value = customer.Id
            };


            // Step 4: Invoice is always created for an item so lets retrieve reference to an item and a Line item to the invoice
            QueryService<Item> querySvcItem = new QueryService<Item>(serviceContext);
            Item item = querySvcItem.ExecuteIdsQuery("SELECT * FROM Item WHERE Name = 'Lighting'").FirstOrDefault();
            List<Line> lineList = new List<Line>();
            Line line = new Line();
            line.Description = "Description";
            line.Amount = new Decimal(100.00);
            line.AmountSpecified = true;
            lineList.Add(line);
            invoice.Line = lineList.ToArray();

            SalesItemLineDetail salesItemLineDetail = new SalesItemLineDetail();
            salesItemLineDetail.Qty = new Decimal(1.0);
            salesItemLineDetail.ItemRef = new ReferenceType()
            {
                Value = item.Id
            };
            line.AnyIntuitObject = salesItemLineDetail;

            line.DetailType = LineDetailTypeEnum.SalesItemLineDetail;
            line.DetailTypeSpecified = true;

            // Step 5: Set other properties such as Total Amount, Due Date, Email status and Transaction Date
            invoice.DueDate = DateTime.UtcNow.Date;
            invoice.DueDateSpecified = true;


            invoice.TotalAmt = new Decimal(10.00);
            invoice.TotalAmtSpecified = true;

            invoice.EmailStatus = EmailStatusEnum.NotSet;
            invoice.EmailStatusSpecified = true;

            invoice.Balance = new Decimal(10.00);
            invoice.BalanceSpecified = true;

            invoice.TxnDate = DateTime.UtcNow.Date;
            invoice.TxnDateSpecified = true;
            invoice.TxnTaxDetail = new TxnTaxDetail()
            {
                TotalTax = Convert.ToDecimal(10),
                TotalTaxSpecified = true,
            };

            // Step 6: Initialize the service object and create Invoice
            DataService service = new DataService(serviceContext);
            Invoice addedInvoice = service.Add<Invoice>(invoice);
        }

        private ServiceContext IntializeContext(string realmId)
        {
            var principal = User as ClaimsPrincipal;
            OAuth2RequestValidator oauthValidator = new OAuth2RequestValidator(principal.FindFirst("access_token").Value);
            ServiceContext serviceContext = new ServiceContext(realmId, IntuitServicesType.QBO, oauthValidator);
            return serviceContext;
        }

        /// <summary>
        /// Revoke access tokens
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> RevokeAccessToken()
        {
            var accessToken = (User as ClaimsPrincipal).FindFirst("access_token").Value;

            //Revoke Access token call
            var revokeClient = new TokenRevocationClient(AppController.revocationEndpoint, clientid, clientsecret);

            //Revoke access token
            TokenRevocationResponse revokeAccessTokenResponse = await revokeClient.RevokeAccessTokenAsync(accessToken);
            if (revokeAccessTokenResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                Session.Abandon();
                Request.GetOwinContext().Authentication.SignOut();
                
            }//delete claims and cookies
           
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Revoke refresh tokens
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> RevokeRefreshToken()
        {
            var refreshToken = (User as ClaimsPrincipal).FindFirst("refresh_token").Value;
            
            //Revoke Refresh token call
            var revokeClient = new TokenRevocationClient(AppController.revocationEndpoint, clientid, clientsecret);

            //Revoke refresh token
            TokenRevocationResponse revokeAccessTokenResponse = await revokeClient.RevokeAccessTokenAsync(refreshToken);
            if (revokeAccessTokenResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                Session.Abandon();
                Request.GetOwinContext().Authentication.SignOut();
            }
            //return RedirectToAction("Index");
            return RedirectToAction("Index");
        }

        //Update cookie with new claim indfo/tokens for logged in user
        private void UpdateCookie(TokenResponse response)
        {
            if (response.IsError)
            {
                throw new Exception(response.Error);
            }

            var identity = (User as ClaimsPrincipal).Identities.First();
            var result = from c in identity.Claims
                         where c.Type != "access_token" &&
                               c.Type != "refresh_token" &&
                               c.Type != "access_token_expires_at" &&
                               c.Type != "access_token_expires_at" 
                         select c;

            var claims = result.ToList();

            claims.Add(new Claim("access_token", response.AccessToken));
           
            claims.Add(new Claim("access_token_expires_at", (DateTime.Now.AddSeconds(response.AccessTokenExpiresIn)).ToString()));
            claims.Add(new Claim("refresh_token", response.RefreshToken));
           
            claims.Add(new Claim("refresh_token_expires_at", (DateTime.UtcNow.ToEpochTime() + response.RefreshTokenExpiresIn).ToDateTimeFromEpoch().ToString()));
           
            var newId = new ClaimsIdentity(claims, "Cookies");
            Request.GetOwinContext().Authentication.SignIn(newId);
        }
        
    }
}