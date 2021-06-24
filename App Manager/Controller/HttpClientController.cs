using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json;

namespace App_Manager.Controller
{
  class HttpClientController
  {
    private IAuthenticationProvider msalProvider;
    private HttpClient httpClient;
    public void InitializeUser(string clientId, string Tenant)
    {
      var clientApplication = PublicClientApplicationBuilder.Create(clientId)
                                              //.WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                                              .WithRedirectUri("http://localhost")
                                              .WithAuthority(AzureCloudInstance.AzurePublic, Tenant)
                                              .Build();
      List<string> scopes = new List<string>();
      scopes.Add("application.readwrite.all");
      scopes.Add("approleassignment.readwrite.all");
      string accessToken = clientApplication.AcquireTokenInteractive(scopes).ExecuteAsync().Result.AccessToken;
      this.msalProvider = new MsalAuthenticationProvider(clientApplication, scopes.ToArray());
      this.httpClient = new HttpClient(new AuthHandler(this.msalProvider, new HttpClientHandler()));
    }

    public async Task<Model.Application> CreateApplication(string appName)
    {
      Uri uri = new Uri("https://graph.microsoft.com/v1.0/applications");
      var application = new { displayName = appName };
      string contentString = JsonConvert.SerializeObject(application);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      var httpResult = await this.httpClient.PostAsync(uri, data);
      string result = await httpResult.Content.ReadAsStringAsync();
      var newApp = JsonConvert.DeserializeObject<Model.Application>(result);
      return newApp;
    }

    public async Task<string> CreateServicePrincipal(string appID)
    {
      Uri uri = new Uri("https://graph.microsoft.com/v1.0/servicePrincipals");
      var servicePrincipal = new { appId = appID };
      string contentString = JsonConvert.SerializeObject(servicePrincipal);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      var httpResult = await httpClient.PostAsync(uri, data);
      string result = await httpResult.Content.ReadAsStringAsync();
      var newServicePrincipal = JsonConvert.DeserializeObject<ServicePrincipal>(result);
      return newServicePrincipal.Id;
    }

    public async Task<Model.ApplicationServicePrincipal> InstantiateAppTemplate(string templateID, string appName)
    {
      Uri uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/applicationTemplates/{0}/instantiate", templateID));
      var application = new { displayName = appName };
      string contentString = JsonConvert.SerializeObject(application);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      var httpResult = await this.httpClient.PostAsync(uri, data);
      string result = await httpResult.Content.ReadAsStringAsync();
      var newApp = JsonConvert.DeserializeObject<Model.ApplicationServicePrincipal>(result);
      return newApp;
    }

    public async Task<string> AddUnifiedRoleAssignment(string roleId, string principalID, string userLogin) 
    {
      Uri uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/users/{0}", userLogin));
      var httpStringResult = httpClient.GetStringAsync(uri).Result;
      var user = JsonConvert.DeserializeObject<User>(httpStringResult);
      var roleAssgn = new
      {
        principalId = user.Id,
        resourceScope = String.Format("/{0}", principalID),
        directoryScopeId = String.Format("/{0}", principalID),
        roleDefinitionId = roleId
      };
      string contentString = JsonConvert.SerializeObject(roleAssgn);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      uri = new Uri("https://graph.microsoft.com/beta/roleManagement/directory/roleAssignments");
      var httpResult = await httpClient.PostAsync(uri, data);
      string result = await httpResult.Content.ReadAsStringAsync();
      var newUnifiedRoleAssignment = JsonConvert.DeserializeObject<UnifiedRoleAssignment>(result);
      return newUnifiedRoleAssignment.Id;
    }
    public async Task<bool> AddAppRole(string appID, IEnumerable<AppRole> existingAppRoles, string roleName, Guid roleID)
    {
      
      AppRole role = new AppRole
      {
        Id = roleID,
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
      Uri uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/applications/{0}", appID));
      // Iterate till app available
      for (int i = 1; i <= 5; i++)
      {
        var httpGetResult = await httpClient.GetAsync(uri);
        if (httpGetResult.StatusCode != HttpStatusCode.NotFound)
        {
          break;
        }
        else
        {
          Thread.Sleep(3000);
        }
      }      
      string contentString = JsonConvert.SerializeObject(appUpdate);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      var httpResult = httpClient.PatchAsync(uri, data).Result;
      if (httpResult.StatusCode == HttpStatusCode.NoContent)
      {
        return true;
      }
      else
      {
        return false;
      }
    }

    public async Task<string> AddAppRoleAssignment(string roleId, string principalID, string userLogin)
    {
      Uri uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/users/{0}", userLogin));
      var httpStringResult = httpClient.GetStringAsync(uri).Result;
      var user = JsonConvert.DeserializeObject<User>(httpStringResult);
      var roleAssgn = new 
      {
        principalId = user.Id,
        resourceId = principalID,
        appRoleId = roleId
      };
      string contentString = JsonConvert.SerializeObject(roleAssgn);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      // uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/users/{0}/appRoleAssignments", user.Id));
      uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/servicePrincipals/{0}/appRoleAssignedTo", principalID));
      var httpResult = httpClient.PostAsync(uri, data).Result;
      string result = httpResult.Content.ReadAsStringAsync().Result;
      var newAppRoleAssgnmnt = JsonConvert.DeserializeObject<AppRoleAssignment>(result);
      return newAppRoleAssgnmnt.Id;
    }

    public async Task<bool> AssignClaimsMappingPolicy(string principalID)
    {
      // string policyID = "b155df89-b8f3-4e1b-846f-d8622e6131d1";
      string policyID = "f5359865-f8fc-4bca-8659-523286d3babe"; // Siemens Demo Tenant
      string refPolicy = String.Format("https://graph.microsoft.com/v1.0/policies/claimsMappingPolicies/{0}", policyID);
      var claimsMappingPolicy = new ClaimsMappingPolicy
      {
        AdditionalData = new Dictionary<string, object>()
        {
          {"@odata.id", refPolicy}
        }
      };
      string contentString = JsonConvert.SerializeObject(claimsMappingPolicy);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      Uri uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/servicePrincipals/{0}/claimsMappingPolicies/$ref", principalID));
      var httpResult = await httpClient.PostAsync(uri, data);
      string result = await httpResult.Content.ReadAsStringAsync();
      if (httpResult.StatusCode == HttpStatusCode.NoContent)
      {
        return true;
      }
      else
      {
        return false;
      }
    }

    public async Task<bool> AssignConditionalAccessPolicy(string appID)
    {
      string policyID = "78bd0c80-bbed-4d76-9e24-84c31b1101c3"; // Siemens Demo "Policy_TrustedDevice_AppA"
      Uri uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/identity/conditionalAccess/policies/{0}", policyID));
      var httpStringResult = httpClient.GetStringAsync(uri).Result;
      var policy = JsonConvert.DeserializeObject<Microsoft.Graph.ConditionalAccessPolicy>(httpStringResult);

      var existingApplications = policy.Conditions.Applications.IncludeApplications;
      List<string> newIncludeApplications = new List<string>();
      foreach (string existingApp in existingApplications)
      {
        if (await this.ValidateApplication(existingApp)) // In fact thats the appId
        {
          newIncludeApplications.Add(existingApp);
        }
      }
      newIncludeApplications.Add(appID);
      ConditionalAccessApplications newApplications = new ConditionalAccessApplications
      {
        IncludeApplications = newIncludeApplications
      };
      ConditionalAccessConditionSet newConditions = new ConditionalAccessConditionSet
      {
        Applications = newApplications
      };
      ConditionalAccessPolicy newPolicy = new ConditionalAccessPolicy
      {
        Conditions = newConditions        
      };
      string contentString = JsonConvert.SerializeObject(newPolicy);
      var data = new StringContent(contentString, Encoding.UTF8, "application/json");
      var httpResult = await httpClient.PatchAsync(uri, data);
      if (httpResult.StatusCode == HttpStatusCode.NoContent)
      {
        return true;
      }
      else
      {
        string result = await httpResult.Content.ReadAsStringAsync();
        Console.WriteLine("Error: " + result);
        return false;
      }
    }

    private async Task<bool> ValidateApplication(string appID)
    {
      Uri uri = new Uri(String.Format("https://graph.microsoft.com/v1.0/applications?$filter=appId eq '{0}'", appID));
      var httpGetResult = await httpClient.GetAsync(uri);
      string result = await httpGetResult.Content.ReadAsStringAsync();
      var apps = JsonConvert.DeserializeObject<Model.ApplicationCollection>(result);
      if (apps.value.Length < 1)
      { 
        return false;
      }
      else
      {
        return true;
      }
    }
  }
}
