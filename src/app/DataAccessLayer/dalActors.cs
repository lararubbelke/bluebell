using Helium.Model;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Helium.DataAccessLayer
{
    /// <summary>
    /// Data Access Layer for CosmosDB
    /// </summary>
    public partial class DAL
    {
        // select template for Actors
        const string _actorSelect = "select m.id, m.partitionKey, m.actorId, m.type, m.name, m.birthYear, m.deathYear, m.profession, m.textSearch, m.movies from m where m.type = 'Actor' ";
        const string _actorOrderBy = " order by m.name";
        const string _actorOffset = " offset {0} limit {1}";

        /// <summary>
        /// Retrieve a single Actor from CosmosDB by actorId
        /// 
        /// Uses the CosmosDB single document read API which is 1 RU if less than 1K doc size
        /// 
        /// Throws an exception if not found
        /// </summary>
        /// <param name="actorId">Actor ID</param>
        /// <returns>Actor object</returns>
        public async System.Threading.Tasks.Task<Actor> GetActorAsync(string actorId)
        {
            // get the partition key for the actor ID
            // note: if the key cannot be determined from the ID, ReadDocumentAsync cannot be used.
            // GetPartitionKey will throw an ArgumentException if the actorId isn't valid
            // get an actor by ID
            return await _cosmosDetails.Container.ReadItemAsync<Actor>(actorId, new PartitionKey(GetPartitionKey(actorId)));
        }

        /// <summary>
        /// Get all Actors from CosmosDB
        /// </summary>
        /// <param name="offset">zero based offset for paging</param>
        /// <param name="limit">number of documents for paging</param>
        /// <returns>List of Actors</returns>
        public async Task<IEnumerable<Actor>> GetActorsAsync(int offset = 0, int limit = 0)
        {
            // get all actors
            return await GetActorsByQueryAsync(string.Empty, offset, limit);
        }

        /// <summary>
        /// Get a list of Actors by search string
        /// 
        /// The search is a "contains" search on actor name
        /// If q is empty, all actors are returned
        /// </summary>
        /// <param name="q">search term</param>
        /// <param name="offset">zero based offset for paging</param>
        /// <param name="limit">number of documents for paging</param>
        /// <returns>List of Actors or an empty list</returns>
        public async Task<IEnumerable<Actor>> GetActorsByQueryAsync(string q, int offset = 0, int limit = Constants.DefaultPageSize)
        {
            string sql = _actorSelect;
            string orderby = _actorOrderBy;

            if (limit < 1)
            {
                limit = Constants.DefaultPageSize;
            }
            else if (limit > Constants.MaxPageSize)
            {
                limit = Constants.MaxPageSize;
            }

            string offsetLimit = string.Format(_actorOffset, offset, limit);

            if (!string.IsNullOrEmpty(q))
            {
                // convert to lower and escape embedded '
                q = q.Trim().ToLower().Replace("'", "''");

                if (!string.IsNullOrEmpty(q))
                {
                    // get actors by a "like" search on name
                    sql += string.Format($" and contains(m.textSearch, '{q}') ");
                }
            }

            sql += orderby + offsetLimit;

            return await QueryActorWorkerAsync(sql);
        }

        /// <summary>
        /// Actor worker query
        /// </summary>
        /// <param name="sql">select statement to execute</param>
        /// <returns>List of Actors or empty list</returns>
        public async Task<IEnumerable<Actor>> QueryActorWorkerAsync(string sql)
        {
            // run query
            var query = _cosmosDetails.Container.GetItemQueryIterator<Actor>(sql, requestOptions: _cosmosDetails.QueryRequestOptions);

            List<Actor> results = new List<Actor>();

            while (query.HasMoreResults)
            {
                foreach (var doc in await query.ReadNextAsync())
                {
                    results.Add(doc);
                }
            }
            return results;
        }
    }
}