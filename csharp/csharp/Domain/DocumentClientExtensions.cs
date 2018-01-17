using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host;

using HttpStatusCode = System.Net.HttpStatusCode;

namespace csharp
{
    public static class DocumentClientExtensions
    {

        static RequestOptions permissionRequestOptions(int durationInSeconds) => new RequestOptions { ResourceTokenExpirySeconds = durationInSeconds };

        static string GetUserPermissionId(string databaseId, string userId, PermissionMode permissionMode) => $"{databaseId}-{userId}-{permissionMode.ToString().ToUpper()}";


        public static async Task<Permission> GetOrCreatePermission(this DocumentClient client, (string DatabaseId, string CollectionId) collection, string userId, PermissionMode permissionMode, int durationInSeconds, TraceWriter log = null)
        {
            var permissionId = string.Empty;

            try
            {
                await client.EnsureCollection(collection);

                log?.Info($"Attempting to get Document Collection in Database {collection.DatabaseId} with CollectionId: {collection.CollectionId}");

                var collectionResponse = await client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(collection.DatabaseId, collection.CollectionId));

                var documentCollection = collectionResponse?.Resource ?? throw new Exception($"Could not find Document Collection in Database {collection.DatabaseId} with CollectionId: {collection.CollectionId}");


                var userTup = await client.GetOrCreateUser(collection.DatabaseId, userId, log);

                var user = userTup.user;

                Permission permission;

                permissionId = GetUserPermissionId(collection.DatabaseId, user.Id, permissionMode);

                // if the user was newly created, go ahead and create the permission
                if (userTup.created && !string.IsNullOrEmpty(user?.Id))
                {
                    permission = await client.CreateNewPermission(collection.DatabaseId, documentCollection, user, permissionId, permissionMode, durationInSeconds, log);
                }
                else // else look for an existing permission with the id
                {
                    var permissionUri = UriFactory.CreatePermissionUri(collection.DatabaseId, user.Id, permissionId);

                    try
                    {
                        //int count = 0;

                        //string continuation = string.Empty;

                        //do
                        //{
                        //  // Read the feed 10 items at a time until there are no more items to read
                        //  var r = await client.ReadPermissionFeedAsync (user.PermissionsLink, new FeedOptions { MaxItemCount = -1, RequestContinuation = continuation });

                        //  // Append the item count
                        //  count += r.Count;

                        //  // Get the continuation so that we know when to stop.
                        //  continuation = r.ResponseContinuation;

                        //  foreach (var i in r)
                        //  {
                        //      log?.Info ($"permission.Id:             {i.Id}");
                        //      log?.Info ($"permission.ResourceId:     {i.ResourceId}");
                        //      log?.Info ($"permission.ResourceLink:   {i.ResourceLink}");
                        //      log?.Info ($"permission.AltLink:        {i.AltLink}");
                        //      log?.Info ($"permission.PermissionMode: {i.PermissionMode}");
                        //      log?.Info ($"permission.SelfLink:       {i.SelfLink}");
                        //      log?.Info ($"permission.Timestamp:      {i.Timestamp}");
                        //      log?.Info ($"permission.Token:          {i.Token}");
                        //      log?.Info ($"permission.ETag:           {i.ETag}");
                        //      log?.Info ($"");
                        //  }

                        //} while (!string.IsNullOrEmpty (continuation));


                        log?.Info($"Attempting to get Permission with Id: {permissionId}  at Uri: {permissionUri}");

                        var permissionResponse = await client.ReadPermissionAsync(permissionUri, permissionRequestOptions(durationInSeconds));

                        permission = permissionResponse?.Resource;

                        if (permission != null)
                        {
                            log?.Info($"Found existing Permission with Id: {permission.Id}");
                        }
                    }
                    catch (DocumentClientException dcx)
                    {
                        dcx.Print(log);

                        switch (dcx.StatusCode)
                        {
                            case HttpStatusCode.NotFound:

                                log?.Info($"Did not find Permission with Id: {permissionId}  at Uri: {permissionUri} - creating...");

                                permission = await client.CreateNewPermission(collection.DatabaseId, documentCollection, user, permissionId, permissionMode, durationInSeconds, log);

                                break;

                            default: throw;
                        }
                    }
                }

                return permission;
            }
            catch (Exception ex)
            {
                log?.Error($"Error creating new new {permissionMode.ToString().ToUpper()} Permission [Database: {collection.DatabaseId} Collection: {collection.CollectionId}  User: {userId}  Permission: {permissionId}", ex);
                throw;
            }
        }



        static async Task<Permission> CreateNewPermission(this DocumentClient client, string databaseId, DocumentCollection collection, User user, string permissionId, PermissionMode permissionMode, int durationInSeconds, TraceWriter log = null)
        {
            log?.Info($"Creating new Permission with Id: {permissionId}  for Collection: {collection?.Id}");

            var newPermission = new Permission { Id = permissionId, ResourceLink = collection.SelfLink, PermissionMode = permissionMode };

            try
            {
                var permissionResponse = await client.CreatePermissionAsync(user.SelfLink, newPermission, permissionRequestOptions(durationInSeconds));

                var permission = permissionResponse?.Resource;

                if (permission != null)
                {
                    log?.Info($"Created new Permission with Id: {permission.Id}");
                }

                return permission;
            }
            catch (DocumentClientException dcx)
            {
                dcx.Print(log);

                switch (dcx.StatusCode)
                {
                    case HttpStatusCode.Conflict:

                        var oldPermissionId = permissionId.Replace(permissionMode.ToString().ToUpper(), permissionMode == PermissionMode.All ? PermissionMode.Read.ToString().ToUpper() : PermissionMode.All.ToString().ToUpper());

                        log?.Info($"Deleting old Permission with Id: {oldPermissionId}...");

                        await client.DeletePermissionAsync(UriFactory.CreatePermissionUri(databaseId, user.Id, oldPermissionId));

                        log?.Info($"Creating new Permission with Id: {permissionId}  for Collection: {collection?.Id}");

                        var permissionResponse = await client.CreatePermissionAsync(user.SelfLink, newPermission, permissionRequestOptions(durationInSeconds));

                        var permission = permissionResponse?.Resource;

                        if (permission != null)
                        {
                            log?.Info($"Created new Permission with Id: {permission.Id}");
                        }

                        return permission;

                    default: throw;
                }
            }
            catch (Exception ex)
            {
                log?.Error($"Error creating new Permission with Id: {permissionId}  for Collection: {collection?.Id}", ex);
                throw;
            }
        }


        static async Task<(User user, bool created)> GetOrCreateUser(this DocumentClient client, string databaseId, string userId, TraceWriter log = null)
        {
            User user = null;

            try
            {
                log?.Info($"Attempting to get Database ({databaseId}) User with Id: {userId}");

                var response = await client.ReadUserAsync(UriFactory.CreateUserUri(databaseId, userId));

                user = response?.Resource;

                if (user != null)
                {
                    log?.Info($"Found existing Database ({databaseId}) User with Id {userId}");
                }

                return (user, false);
            }
            catch (DocumentClientException dcx)
            {
                dcx.Print(log);

                switch (dcx.StatusCode)
                {
                    case HttpStatusCode.NotFound:

                        log?.Info($"Did not find user with Id {userId} - creating...");

                        var response = await client.CreateUserAsync(UriFactory.CreateDatabaseUri(databaseId), new User { Id = userId });

                        user = response?.Resource;

                        if (user != null)
                        {
                            log?.Info($"Created new Database ({databaseId}) User with Id {userId}");
                        }

                        return (user, user != null);

                    default: throw;
                }
            }
            catch (Exception ex)
            {
                log?.Error($"Error getting User with Id: {userId}\n", ex);
                throw;
            }
        }


        public static void Print(this DocumentClientException dex, TraceWriter log)
        {
            if ((int)dex.StatusCode == 429)
            {
                log?.Info("TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.");
            }
            else
            {
                switch (dex.StatusCode)
                {
                    case HttpStatusCode.BadRequest:
                        log?.Info("BadRequest - This means something was wrong with the document supplied. It is likely that disableAutomaticIdGeneration was true and an id was not supplied");
                        break;
                    case HttpStatusCode.Forbidden:
                        log?.Info("Forbidden - This likely means the collection in to which you were trying to create the document is full.");
                        break;
                    case HttpStatusCode.Conflict:
                        log?.Info("Conflict - This means a Document with an id matching the id field of document already existed");
                        break;
                    case HttpStatusCode.RequestEntityTooLarge:
                        log?.Info("RequestEntityTooLarge - This means the Document exceeds the current max entity size. Consult documentation for limits and quotas.");
                        break;
                    default:
                        break;
                }
            }
        }


        #region Initialization (database & collections)

        static readonly Dictionary<string, ClientStatus> _databaseStatuses = new Dictionary<string, ClientStatus>();

        static readonly Dictionary<string, Task<ResourceResponse<Database>>> _databaseCreationTasks = new Dictionary<string, Task<ResourceResponse<Database>>>();


        static readonly Dictionary<(string DatabaseId, string CollectionId), ClientStatus> _collectionStatuses = new Dictionary<(string DatabaseId, string CollectionId), ClientStatus>();

        static readonly Dictionary<(string DatabaseId, string CollectionId), Task<ResourceResponse<DocumentCollection>>> _collectionCreationTasks = new Dictionary<(string DatabaseId, string CollectionId), Task<ResourceResponse<DocumentCollection>>>();


        static bool IsInitialized((string DatabaseId, string CollectionId) collection) => _collectionStatuses.TryGetValue(collection, out ClientStatus status) && status == ClientStatus.Initialized;


        static async Task EnsureCollection(this DocumentClient client, (string DatabaseId, string CollectionId) collection)
        {
            if (!(IsInitialized(collection) || await client.InitializeCollection(collection)))
            {
                throw new Exception($"Could not find Document Collection in Database {collection.DatabaseId} with CollectionId: {collection.CollectionId}");
            }
        }


        static async Task<bool> InitializeCollection(this DocumentClient client, (string DatabaseId, string CollectionId) collection, TraceWriter log = null)
        {
            if (!(_databaseStatuses.TryGetValue(collection.DatabaseId, out ClientStatus databaseStatus) && databaseStatus == ClientStatus.Initialized))
            {
                await client.CreateDatabaseIfNotExistsAsync(collection.DatabaseId, log);
            }

            if (!IsInitialized(collection))
            {
                await client.CreateCollectionIfNotExistsAsync(collection, log);
            }

            return IsInitialized(collection);
        }


        static async Task CreateDatabaseIfNotExistsAsync(this DocumentClient client, string databaseId, TraceWriter log = null)
        {
            if (_databaseCreationTasks.TryGetValue(databaseId, out Task<ResourceResponse<Database>> task) && !task.IsNullFinishCanceledOrFaulted())
            {
                log?.Info("Database is already being created, returning existing task...");

                await task;
            }
            else
            {
                try
                {
                    log?.Info("Checking for Database...");

                    _databaseCreationTasks[databaseId] = client.ReadDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId));

                    var database = await _databaseCreationTasks[databaseId];

                    if (database?.Resource != null)
                    {
                        _databaseStatuses[databaseId] = ClientStatus.Initialized;

                        log?.Info($"Found existing Database");
                    }
                }
                catch (DocumentClientException dex)
                {
                    switch (dex.StatusCode)
                    {
                        case HttpStatusCode.NotFound:

                            _databaseCreationTasks[databaseId] = client.CreateDatabaseAsync(new Database { Id = databaseId });

                            var database = await _databaseCreationTasks[databaseId];

                            if (database?.Resource != null)
                            {
                                _databaseStatuses[databaseId] = ClientStatus.Initialized;

                                log?.Info($"Created new Database");
                            }

                            break;

                        default: throw;
                    }
                }
                catch (Exception ex)
                {
                    _databaseStatuses[databaseId] = ClientStatus.NotInitialized;

                    log?.Error(ex.Message, ex);

                    throw;
                }
            }
        }


        static async Task CreateCollectionIfNotExistsAsync<T>(this DocumentClient client, string databaseId, TraceWriter log = null)
        {
            await client.CreateCollectionIfNotExistsAsync((databaseId, typeof(T).Name), log);
        }


        static async Task CreateCollectionIfNotExistsAsync(this DocumentClient client, (string DatabaseId, string CollectionId) collection, TraceWriter log = null)
        {
            if (_collectionCreationTasks.TryGetValue(collection, out Task<ResourceResponse<DocumentCollection>> task) && !task.IsNullFinishCanceledOrFaulted())
            {
                log?.Info($"Collection: {collection.CollectionId} in Database: {collection.DatabaseId} is already being created, returning existing task...");

                await task;
            }
            else
            {
                try
                {
                    log?.Info($"Checking for Collection: {collection.CollectionId} in Database: {collection.DatabaseId}...");

                    _collectionCreationTasks[collection] = client.ReadDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(collection.DatabaseId, collection.CollectionId));

                    var collectionResponse = await _collectionCreationTasks[collection];

                    if (collectionResponse?.Resource != null)
                    {
                        _collectionStatuses[collection] = ClientStatus.Initialized;

                        log?.Info($"Found existing Collection: {collection.CollectionId} in Database: {collection.DatabaseId}");
                    }
                }
                catch (DocumentClientException dex)
                {
                    switch (dex.StatusCode)
                    {
                        case HttpStatusCode.NotFound:

                            _collectionCreationTasks[collection] = client.CreateDocumentCollectionAsync(UriFactory.CreateDatabaseUri(collection.DatabaseId), new DocumentCollection { Id = collection.CollectionId }, new RequestOptions { OfferThroughput = 1000 });

                            var collectionResponse = await _collectionCreationTasks[collection];

                            if (collectionResponse?.Resource != null)
                            {
                                _collectionStatuses[collection] = ClientStatus.Initialized;

                                log?.Info($"Created new Collection: {collection.CollectionId} in Database: {collection.DatabaseId}");
                            }

                            break;

                        default: throw;
                    }
                }
                catch (Exception ex)
                {
                    _collectionStatuses[collection] = ClientStatus.NotInitialized;

                    log?.Error(ex.Message, ex);

                    throw;
                }
            }
        }

        #endregion
    }

    public enum ClientStatus
    {
        NotInitialized,
        Initializing,
        Initialized
    }
}
