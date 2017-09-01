using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ZhihuDatabase
{
    public class DatabaseQuery
    {
        private readonly MongoClient _client;
        private readonly string _dbName;
        private readonly string _collectionName;
        private readonly BsonDocument _filter;

        private ObjectId[] _lastIds;
        private DateTime _queryTime;

        public int PageSize { get; }
        public int CurrentPageNumber { get; private set; }
        public int PageCount { get; private set; }
        
        public DatabaseQuery(MongoClient mongoClient, string databaseName, string collectionName, 
            BsonDocument filter, int pageSize)
        {
            _client = mongoClient;
            _dbName = databaseName;
            _collectionName = collectionName;
            _filter = filter;
            
            PageSize = pageSize;
        }

        public async void Init()
        {
            var db = _client.GetDatabase(_dbName);
            var collection = db.GetCollection<BsonDocument>(_collectionName);
            var resultCount = await Task.Run( () => collection.Find(_filter).Count() );

            CurrentPageNumber = 0;
            PageCount = (int) (Math.Ceiling((float) resultCount) / PageSize);
            
            _lastIds = new ObjectId[PageCount + 1];
            _queryTime = _lastIds[0].CreationTime;
        }

        public async Task<List<BsonDocument>> GetPage(int toPageNumber)
        {
            var db = _client.GetDatabase(_dbName);
            var collection = db.GetCollection<BsonDocument>(_collectionName);

            var skip = 0;

            var filter = new BsonDocument(_filter);
            if (_filter.Contains("_id") || _lastIds[toPageNumber - 1].CreationTime >= _queryTime)
            {
                skip = (toPageNumber - 1) * PageSize;
            }
            else if (toPageNumber < CurrentPageNumber)
            {
                filter.Add("_id", new BsonDocument("$gt", _lastIds[toPageNumber - 1]));
            }
            
            var result = await Task.Run( () => collection.Find(filter).Skip(skip).Limit(PageSize).ToList() );
            CurrentPageNumber = toPageNumber;
            _lastIds[CurrentPageNumber] = result.Last().GetValue("_id").AsObjectId;
            return result;
        }
    }
}