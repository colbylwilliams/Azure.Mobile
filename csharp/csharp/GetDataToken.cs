using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

using HttpStatusCode = System.Net.HttpStatusCode;

namespace csharp
{
    public static class GetDataToken
    {
		const string AnonymousId = "anonymous_user";

		const int TokenDurationSeconds = 18000; // 5 hours
		const double TokenRefreshSeconds = 600; // 10 minutes


		static DocumentClient _documentClient;
		static DocumentClient DocumentClient => _documentClient ?? (_documentClient = new DocumentClient(EnvironmentVariables.DocumentDbUri, EnvironmentVariables.DocumentDbKey));

		static AzureServiceTokenProvider _azureServiceTokenProvider;
		static AzureServiceTokenProvider AzureServiceTokenProvider => _azureServiceTokenProvider ?? (_azureServiceTokenProvider = new AzureServiceTokenProvider());

		static KeyVaultClient _keyVaultClient;
		static KeyVaultClient KeyVaultClient => _keyVaultClient ?? (_keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(AzureServiceTokenProvider.KeyVaultTokenCallback)));


		[Authorize]
		[FunctionName(nameof(GetDataToken))]
        public static async Task<HttpResponseMessage> Run(
        	[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/data/{databaseId}/{collectionId}/token")]
			HttpRequestMessage req, string databaseId, string collectionId, TraceWriter log)
        {
			try
			{
				var userId = Thread.CurrentPrincipal.GetClaimsIdentity()?.UniqueIdentifier() ?? AnonymousId;

				var secretBundle = await KeyVaultClient.GetSecretAsync(EnvironmentVariables.KeyVaultUrl, userId);
				
	
				// if the token is still valid for the next 10 mins return it
				if (secretBundle.Attributes.Expires.HasValue && secretBundle.Attributes.Expires.Value.Subtract(DateTime.UtcNow).TotalSeconds < TokenRefreshSeconds)
				{
					return req.CreateResponse(HttpStatusCode.OK, secretBundle.Value);
				}


				// simply getting the user permission will refresh the token
				var userPermission = await DocumentClient.GetOrCreatePermission((databaseId, collectionId), userId, PermissionMode.All, TokenDurationSeconds, log);


				if (!string.IsNullOrEmpty(userPermission?.Token))
				{
					secretBundle = await KeyVaultClient.SetSecretAsync(EnvironmentVariables.KeyVaultUrl, userId, userPermission.Token, secretAttributes: new SecretAttributes(expires: DateTime.UtcNow.AddSeconds(TokenDurationSeconds)));

					//log.Info($"Updated User Store:\n{userStore}");

					return req.CreateResponse(HttpStatusCode.OK, secretBundle.Value);
				}


				return req.CreateResponse(HttpStatusCode.InternalServerError);
			}
			catch (Exception ex)
			{
				log.Error(ex.Message, ex);

				return req.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
			}
		}
	}
}
