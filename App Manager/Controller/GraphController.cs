using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace App_Manager.Controller
{
  class GraphController
  {
    private GraphServiceClient graphClient;    

    public void Initialize(string clientId, string authority, string clientSecret)
    {
      var clientApplication = ConfidentialClientApplicationBuilder.Create(clientId)
                                              .WithAuthority(authority)
                                              .WithClientSecret(clientSecret)
                                              .Build();
      List<string> scopes = new List<string>();
      scopes.Add("https://graph.microsoft.com/.default");
      string accessToken = clientApplication.AcquireTokenForClient(scopes).ExecuteAsync().Result.AccessToken;
      GraphServiceClient graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                      requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                    }));      
      this.graphClient = graphClient;
    }

    public void InitializeUser(string clientId, string authority, string Tenant)
    {
      var clientApplication = PublicClientApplicationBuilder.Create(clientId)
                                              //.WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                                              .WithRedirectUri("http://localhost")
                                              .WithAuthority(AzureCloudInstance.AzurePublic, Tenant)
                                              .Build();
      List<string> scopes = new List<string>();
      scopes.Add("application.readwrite.all");
      string accessToken = clientApplication.AcquireTokenInteractive(scopes).ExecuteAsync().Result.AccessToken;
      GraphServiceClient graphClient = new GraphServiceClient(new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                      requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                    }));
      this.graphClient = graphClient;
    }

    public async Task<Model.Application> CreateApplication(string appName)
    {
      var app = new Application
      {
        DisplayName = appName,
        SignInAudience = "AzureADMyOrg", // AzureADMultipleOrgs
        Tags = new string[] { "App Manager Demo" }
      };
      var createdApp = await this.graphClient.Applications.Request().AddAsync(app);
      return new Model.Application
      {
        AppId = createdApp.AppId,
        Id = createdApp.Id
      };
    }

    public async Task<string> CreateServicePrincipal(string appID)
    {
      var sp = new ServicePrincipal
      {
        AppId = appID
      };
      var createdServicePrincipal = await this.graphClient.ServicePrincipals.Request().AddAsync(sp);
      return createdServicePrincipal.Id;
    }

    public async Task<string> AddDelegatedPermission(string appID, string scope)
    {
      var oAuth2PermissionGrant = new OAuth2PermissionGrant
      {
        ClientId = appID,
        ConsentType = "AllPrincipals",
        PrincipalId = null,
        ResourceId = "fe7b4835-3092-447e-9e2a-928d30421f0a", // Always Microsoft Graph, eval/set initially
        Scope = scope
      };

      OAuth2PermissionGrant permissionGrant = await this.graphClient.Oauth2PermissionGrants.Request().AddAsync(oAuth2PermissionGrant);

      return permissionGrant.Id;
    }

    public async Task<string> AddApplicationPermission(string appID, string scope)
    {
      string resourceId = "fe7b4835-3092-447e-9e2a-928d30421f0a"; // Always Microsoft Graph, eval/set initially
      var graphServicePrincipal = await this.graphClient.ServicePrincipals[resourceId].Request().Select("AppRoles").GetAsync();
      Guid? appRoleID = null;
      foreach (AppRole appRole in graphServicePrincipal.AppRoles)
      {
        if (appRole.Value == scope)
        {
          appRoleID = appRole.Id;
        }
      }
      var appRoleAssignment = new AppRoleAssignment
      {
        PrincipalId = Guid.Parse(appID),
        ResourceId = Guid.Parse(resourceId),
        AppRoleId = appRoleID
      };

      AppRoleAssignment permissionGrant = await this.graphClient.ServicePrincipals[appID].AppRoleAssignments.Request().AddAsync(appRoleAssignment);

      return permissionGrant.Id;
    }

    public async Task<string> AddApplicationSecret(string appID, int daysValid)
    {
      PasswordCredential pwdc = new PasswordCredential
      {
        DisplayName = "",
        StartDateTime = DateTimeOffset.Now,
        EndDateTime = DateTimeOffset.Now.AddDays(daysValid)
      };
      var passwordCredential = await this.graphClient.Applications[appID].AddPassword(pwdc).Request().PostAsync();
      return passwordCredential.SecretText;
    }

    public async Task<string> AddAppRole(string appID, string roleName)
    {
      Guid guid = Guid.NewGuid();
      var existingApp = await this.graphClient.Applications[appID].Request().GetAsync();
      var existingAppRoles = existingApp.AppRoles;
      AppRole role = new AppRole
      {
        Id = guid,
        DisplayName = roleName,
        Description = "For " + roleName,
        IsEnabled = true,
        AllowedMemberTypes = new string[] { "User" }
      };
      List<AppRole> newRoleCollection = new List<AppRole>();
      newRoleCollection.AddRange(existingAppRoles);
      newRoleCollection.Add(role);
      Application appUpdate = new Application
      {
        Id = appID,
        AppRoles = newRoleCollection // new AppRole[] { role }
      };
      var updatedApp = await this.graphClient.Applications[appID].Request().UpdateAsync(appUpdate);
      return guid.ToString();
    }

    public async Task<string> AddAppRoleAssignment(string roleId, string principalID, string userLogin)
    {
      var user = await this.graphClient.Users[userLogin].Request().Select("Id").GetAsync();
      AppRoleAssignment roleAssgn = new AppRoleAssignment
      {
        PrincipalId = new Guid(user.Id),
        ResourceId = new Guid(principalID),
        AppRoleId = new Guid(roleId)
      };
      var addedRoleAssn = await this.graphClient.Users[user.Id].AppRoleAssignments.Request().AddAsync(roleAssgn);
      return addedRoleAssn.Id;
    }

    #region AppTemplates
    public async Task<ApplicationServicePrincipal> InstantiateAppTemplate(string templateID, string appName)
    {
      var appSvcPcpl = await this.graphClient.ApplicationTemplates[templateID].Instantiate(appName).Request().PostAsync();
      return appSvcPcpl;
    }
    #endregion
  }
}
