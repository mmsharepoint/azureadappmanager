using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace App_Manager
{
  public class MsalAuthenticationProvider : IAuthenticationProvider
  {
    private IPublicClientApplication _clientApplication;
    private string[] _scopes;

    public MsalAuthenticationProvider(IPublicClientApplication clientApplication, string[] scopes)
    {
      _clientApplication = clientApplication;
      _scopes = scopes;
    }

    /// <summary>
    /// Update HttpRequestMessage with credentials
    /// </summary>
    public async Task AuthenticateRequestAsync(HttpRequestMessage request)
    {
      var token = await GetTokenAsync();
      request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
    }

    /// <summary>
    /// Acquire Token 
    /// </summary>
    public async Task<string> GetTokenAsync()
    {
      AuthenticationResult authResult = null;
      try
      {
        var accounts = await _clientApplication.GetAccountsAsync();
        authResult = await _clientApplication.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
      }
      catch (MsalUiRequiredException)
      {
        authResult = await _clientApplication.AcquireTokenInteractive(_scopes)
                          .ExecuteAsync();
      }
      
      return authResult.AccessToken;
    }
  }
}
