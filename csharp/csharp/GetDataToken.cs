﻿using System;

using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace csharp
{
    public static class GetDataToken
    {
        const string AnonymousId = "anonymous-user";

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
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/data/{databaseId}/{collectionId}/token")]
            HttpRequest req, string databaseId, string collectionId, TraceWriter log)
        {
            try
            {
                SecretBundle secretBundle = null;

                var userId = Thread.CurrentPrincipal.GetClaimsIdentity()?.UniqueIdentifier() ?? AnonymousId;

                log.Info($"\tuserId: {userId}");


                var secretId = GetSecretName(databaseId, collectionId, userId);

                log.Info($"\tsecretId: {secretId} ({secretId.Length})");


                try
                {
                    secretBundle = await KeyVaultClient.GetSecretAsync(EnvironmentVariables.KeyVaultUrl, secretId);
                }
                catch (KeyVaultErrorException kvex)
                {
                    if (kvex.Body.Error.Code != "SecretNotFound")
                    {
                        throw;
                    }

                    log.Info($"\texisting secret not found");
                }


                // if the token is still valid for the next 10 mins return it
                if (secretBundle != null
                    && secretBundle.Attributes.Expires.HasValue
                    && secretBundle.Attributes.Expires.Value.Subtract(DateTime.UtcNow).TotalSeconds < TokenRefreshSeconds)
                {
                    log.Info($"\texisting secret found with greater than 10 minutes remaining before expiration");

                    return new OkObjectResult(secretBundle.Value);
                }


                log.Info($"\tgetting new permission token for user");

                // simply getting the user permission will refresh the token
                var userPermission = await DocumentClient.GetOrCreatePermission((databaseId, collectionId), userId, PermissionMode.All, TokenDurationSeconds, log);


                if (!string.IsNullOrEmpty(userPermission?.Token))
                {
                    log.Info($"\tsaving new permission token to key vault");

                    secretBundle = await KeyVaultClient.SetSecretAsync(EnvironmentVariables.KeyVaultUrl, secretId, userPermission.Token, secretAttributes: new SecretAttributes(expires: DateTime.UtcNow.AddSeconds(TokenDurationSeconds)));

                    return new OkObjectResult(secretBundle.Value);
                }


                log.Info($"\tfailed to get new permission token for user");

                return new StatusCodeResult(500);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);

                return new StatusCodeResult(500);
            }
        }

        static string GetSecretName(string databaseId, string collectionId, string userId) => $"{databaseId}-{collectionId}-{userId}";
    }
}
