//using IdentityModel.Client;
using Intuit.Ipp.OAuth2PlatformClient;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using System.Net;
using System.Collections.Generic;

namespace MvcCodeFlowClientManual.Controllers
{
    
    public class HomeController : Controller
    {

        

        DiscoveryClient discoveryClient;
        DiscoveryResponse doc;
        AuthorizeRequest request;
        public static IList<JsonWebKey> keys;
        public static string scope;
        public static string authorizeUrl;

        /// <summary>
        /// Use the Index page of Home controller to get all endpoints from discovery url
        /// </summary>
        /// <returns></returns>
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


            //Assign the Sandbox Discovery url for the Apps' Dev clientid and clientsecret that you use
            //Or
            //Assign the Production Discovery url for the Apps' Production clientid and clientsecret that you use

            //Read discovery url from config
            string discoveryUrl = ConfigurationManager.AppSettings["DiscoveryUrl"];

            if (discoveryUrl != null && AppController.clientid != null && AppController.clientsecret != null)
            {
                discoveryClient = new DiscoveryClient(discoveryUrl);
            }
            else
            {
                Exception ex= new Exception("Discovery Url missing!");
                throw ex;
            }
            doc = await discoveryClient.GetAsync();

            //If discovery url is working then get all endpoints
            if (doc.StatusCode == HttpStatusCode.OK)
            {
                //Authorize endpoint
                AppController.authorizeUrl = doc.AuthorizeEndpoint;

                //Token endpoint
                AppController.tokenEndpoint = doc.TokenEndpoint;

                //Token Revocation enpoint
                AppController.revocationEndpoint = doc.RevocationEndpoint;

                //UserInfo endpoint
                AppController.userinfoEndpoint = doc.UserInfoEndpoint;

                //Issuer endpoint
                AppController.issuerEndpoint = doc.Issuer;

                //JWKS Keys
                AppController.keys = doc.KeySet.Keys;
            }

            //Get mod and exponent value
            foreach (var key in AppController.keys)
            {
                if (key.N != null)
                {
                    //Mod
                    AppController.mod = key.N;
                }
                if (key.N != null)
                {
                    //Exponent
                    AppController.expo = key.E;
                }
            }



                return View();
        }

   

        public ActionResult MyAction(string submitButton)
        {
            switch (submitButton)
            {
                case "Connect to Quickbooks":
                    // delegate sending to C2QB Action
                    return (C2QB());
                
                default:
                    // If they've submitted the form without a submitButton, 
                    // just return the view again.
                    return (View());
            }
        }

        /// <summary>
        /// If you have an app created for Accounting and Payments then send both Scopes in your redirect url
        /// </summary>
        /// <returns></returns>
        private ActionResult C2QB()
        {
            scope = OidcScopes.Accounting.GetStringValue() + " " + OidcScopes.Payment.GetStringValue();
            authorizeUrl = GetAuthorizeUrl(scope);
            // perform the redirect here.
            return Redirect(authorizeUrl);
        }

       


        
        /// <summary>
        /// Create a temp state to add new claim info for logged in user
        /// </summary>
        /// <param name="state"></param>
        private void SetTempState(string state)
        {
            //Assign temp state for the logged in user's claims
            var tempId = new ClaimsIdentity("TempState");
            tempId.AddClaim(new Claim("state", state));
           
            //Sign in with temp state
            Request.GetOwinContext().Authentication.SignIn(tempId);
        }

        /// <summary>
        /// To Initiate the OAuth 2 process redirect your App’s customer to Intuit's OAuth 2.0 server. 
        /// In order to retrieve Intuit's OAuth 2.0 server endpointor authorization_endpoint retrieve 
        /// the authorizationEndpoint URI from the discovery document.
        /// </summary>
        /// <param name="scope"></param>
        /// <returns></returns>
        private string GetAuthorizeUrl(string scope)
        {
            var state = Guid.NewGuid().ToString("N");
        
            SetTempState(state);

            //Make Authorization request which will provide you a code on callback controller 
            var request = new AuthorizeRequest(AppController.authorizeUrl);

            string url = request.CreateAuthorizeUrl(
               clientId: AppController.clientid,
               responseType: OidcConstants.AuthorizeResponse.Code,
               scope: scope,
               redirectUri: AppController.redirectUrl,
               state: state);

            return url;
        }

        
    }
}