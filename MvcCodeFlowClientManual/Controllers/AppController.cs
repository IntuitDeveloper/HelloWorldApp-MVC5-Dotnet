using Intuit.Ipp.OAuth2PlatformClient;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.Net;
using Intuit.Ipp.Core;
using Intuit.Ipp.Data;
using Intuit.Ipp.QueryFilter;
using Intuit.Ipp.Security;
using System.Linq;
using Newtonsoft.Json;

namespace MvcCodeFlowClientManual.Controllers
{
    public class AppController : Controller
    {
        public static string clientid = ConfigurationManager.AppSettings["clientid"];
        public static string clientsecret = ConfigurationManager.AppSettings["clientsecret"];
        public static string redirectUrl = ConfigurationManager.AppSettings["redirectUrl"];

        public static string authorizeUrl = "";
        public static string tokenEndpoint = "";
        public static string code = "";

        public static string access_token = "";
        public static string refresh_token = "";
        DiscoveryClient discoveryClient;
        DiscoveryResponse doc;
        public static string scope;

        /// <summary>
        /// Use the Index page of App controller to get all endpoints from discovery url
        /// </summary>
        public async Task<ActionResult> Index()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Session.Clear();
            Session.Abandon();
            Request.GetOwinContext().Authentication.SignOut("Cookies");

            //Intialize DiscoverPolicy on page load
            DiscoveryPolicy dpolicy = new DiscoveryPolicy();
            dpolicy.RequireHttps = true;
            dpolicy.ValidateIssuerName = true;

            //Read discovery url from config
            string discoveryUrl = ConfigurationManager.AppSettings["DiscoveryUrl"];

            if (discoveryUrl != null && clientid != null && clientsecret != null)
            {
                discoveryClient = new DiscoveryClient(discoveryUrl);
            }
            else
            {
                Exception ex= new Exception("Discovery Url missing!");
                throw ex;
            }
            doc = await discoveryClient.GetAsync();

            //If discovery url is working then get all Authorization endpoints
            if (doc.StatusCode == HttpStatusCode.OK)
            {
                authorizeUrl = doc.AuthorizeEndpoint;
                tokenEndpoint = doc.TokenEndpoint;
            }

            return View();
        }

        /// <summary>
        /// Start Auth flow
        /// </summary>
        public ActionResult InitiateAuth(string submitButton)
        {
            switch (submitButton)
            {
                case "Connect to QuickBooks":
                    scope = OidcScopes.Accounting.GetStringValue();

                    var request = new AuthorizeRequest(authorizeUrl);

                    var state = Guid.NewGuid().ToString("N");
                    SetTempState(state);

                    authorizeUrl = request.CreateAuthorizeUrl(
                       clientId: clientid,
                       responseType: OidcConstants.AuthorizeResponse.Code,
                       scope: scope,
                       redirectUri: redirectUrl,
                       state: state);

                    return Redirect(authorizeUrl);

                default:
                    return (View());
            }
        }

        /// <summary>
        /// QBO API Request
        /// </summary>
        public ActionResult ApiCallService()
        {
            if (Session["realmId"] != null)
            {
                string realmId = Session["realmId"].ToString();
                try
                {
                    var principal = User as ClaimsPrincipal;
                    OAuth2RequestValidator oauthValidator = new OAuth2RequestValidator(principal.FindFirst("access_token").Value);
                    
                    // Create a ServiceContext with Auth tokens and realmId
                    ServiceContext serviceContext = new ServiceContext(realmId, IntuitServicesType.QBO, oauthValidator);
                    serviceContext.IppConfiguration.MinorVersion.Qbo = "23";

                    // Create a QuickBooks QueryService using ServiceContext
                    QueryService<CompanyInfo> querySvc = new QueryService<CompanyInfo>(serviceContext); 
                    CompanyInfo companyInfo = querySvc.ExecuteIdsQuery("SELECT * FROM CompanyInfo").FirstOrDefault();

                    string output = "Company Name: " + companyInfo.CompanyName + " Company Address: " + companyInfo.CompanyAddr.Line1 + ", " + companyInfo.CompanyAddr.City + ", " + companyInfo.CompanyAddr.Country + " " + companyInfo.CompanyAddr.PostalCode;
                    return View("ApiCallService", (object)("QBO API call Successful!! Response: " + output));
                }
                catch (Exception ex)
                {
                    return View("ApiCallService", (object)("QBO API call Failed!" + " Error message: " + ex.Message));
                }
            }
            else
                return View("ApiCallService", (object)"QBO API call Failed!");
        }

        /// <summary>
        /// Use the Index page of App controller to get all endpoints from discovery url
        /// </summary>
        public ActionResult Error()
        {
            return View("Error");
        }

        /// <summary>
        /// Action that takes redirection from Callback URL
        /// </summary>
        public ActionResult Tokens()
        {
            return View("Tokens");
        }

        /// <summary>
        /// Create a temp state to add new claim info for logged in user
        /// </summary>
        private void SetTempState(string state)
        {
            //Assign temp state for the logged in user's claims
            var tempId = new ClaimsIdentity("TempState");
            tempId.AddClaim(new Claim("state", state));
           
            //Sign in with temp state
            Request.GetOwinContext().Authentication.SignIn(tempId);
        }
    }
}