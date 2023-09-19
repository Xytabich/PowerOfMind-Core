using System;
using System.Data;
using System.Data.SQLite;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.Common;
using Vintagestory.Common.Database;

namespace PowerOfMind.Systems.WorldBehaviors
{
	public sealed class WBDB
	{
		private const string TABLE_PREFIX = "pom_wbdb";
		private const string POSITION_PARAM = "@position";
		private const string KEY_PARAM = "@key";
		private const string DATA_PARAM = "@data";
		private const string DATA_SIZE_PARAM = "@datasize";

		private const string WORLD_TABLE = "world";
		private const string CHUNKS_TABLE = "chunks";
		private const string MAP_CHUNKS_TABLE = "mapchunks";
		private const string MAP_REGIONS_TABLE = "mapregions";

		private const string CREATE_POS_TABLE_CMD = $"CREATE TABLE IF NOT EXISTS {TABLE_PREFIX}_{{0}} (\"position\" INTEGER, \"key\" TEXT, \"data\" BLOB, PRIMARY KEY (\"position\", \"key\"));";
		private const string GET_POS_DATA_CMD = $"SELECT \"data\" FROM {TABLE_PREFIX}_{{0}} WHERE \"position\"={POSITION_PARAM} AND \"key\"={KEY_PARAM} LIMIT 1;";
		private const string GET_POS_ROWID_CMD = $"SELECT \"rowid\" FROM {TABLE_PREFIX}_{{0}} WHERE \"position\"={POSITION_PARAM} AND \"key\"={KEY_PARAM} LIMIT 1;";
		private const string SET_POS_DATA_CMD = $"INSERT OR REPLACE INTO {TABLE_PREFIX}_{{0}} (\"position\", \"key\", \"data\") VALUES ({POSITION_PARAM}, {KEY_PARAM}, {DATA_PARAM});";
		private const string SET_POS_DATA_ASYNC_CMD = $"INSERT OR REPLACE INTO {TABLE_PREFIX}_{{0}} (\"position\", \"key\", \"data\") VALUES ({POSITION_PARAM}, {KEY_PARAM}, zeroblob({DATA_SIZE_PARAM})); {GET_POS_ROWID_CMD}";

		private const string CREATE_TABLE_CMD = $"CREATE TABLE IF NOT EXISTS {TABLE_PREFIX}_{{0}} (\"key\" TEXT PRIMARY KEY, \"data\" BLOB);";
		private const string GET_DATA_CMD = $"SELECT \"data\" FROM {TABLE_PREFIX}_{{0}} WHERE \"key\"={KEY_PARAM} LIMIT 1;";
		private const string GET_ROWID_CMD = $"SELECT \"rowid\" FROM {TABLE_PREFIX}_{{0}} WHERE \"key\"={KEY_PARAM} LIMIT 1;";
		private const string SET_DATA_CMD = $"INSERT OR REPLACE INTO {TABLE_PREFIX}_{{0}} (\"key\", \"data\") VALUES ({KEY_PARAM}, {DATA_PARAM});";
		private const string SET_DATA_ASYNC_CMD = $"INSERT OR REPLACE INTO {TABLE_PREFIX}_{{0}} (\"key\", \"data\") VALUES ({KEY_PARAM}, zeroblob({DATA_SIZE_PARAM})); {GET_ROWID_CMD}";

		private static readonly Func<GameDatabase, SQLiteConnection> GetConnection;

		static WBDB()
		{
			var db = Expression.Parameter(typeof(GameDatabase));
			GetConnection = Expression.Lambda<Func<GameDatabase, SQLiteConnection>>(Expression.Field(
				Expression.Convert(Expression.Field(db, "conn"), typeof(SQLiteDbConnectionv2)), "sqliteConn"), db).Compile();
		}

		private SQLiteConnection connection => GetConnection(gameDatabase);
		private readonly GameDatabase gameDatabase;

		public WBDB(GameDatabase gameDatabase)
		{
			this.gameDatabase = gameDatabase;
		}

		internal void CreateTablesIfNotExists()
		{
			var tables = new (string name, bool hasPos)[] {
				(WORLD_TABLE, false),
				(CHUNKS_TABLE, true),
				(MAP_CHUNKS_TABLE, true),
				(MAP_REGIONS_TABLE, true)
			};
			foreach(var table in tables)
			{
				using(var cmd = connection.CreateCommand())
				{
					cmd.CommandText = string.Format(table.hasPos ? CREATE_POS_TABLE_CMD : CREATE_TABLE_CMD, table.name);
					cmd.ExecuteNonQuery();
				}
			}
		}

		#region world
		public byte[] LoadWorldData(string dataKey)
		{
			ValidateDataKey(dataKey);
			return LoadData(WORLD_TABLE, null, dataKey);
		}

		public Task<SQLiteBlob> LoadWorldDataAsync(string dataKey, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return LoadDataAsync(WORLD_TABLE, null, dataKey, cancellationToken);
		}

		public void StoreWorldData(string dataKey, byte[] data)
		{
			ValidateDataKey(dataKey);
			StoreData(WORLD_TABLE, null, dataKey, data);
		}

		public Task<SQLiteBlob> StoreWorldDataAsync(string dataKey, int dataSize, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return StoreDataAsync(WORLD_TABLE, null, dataKey, dataSize, cancellationToken);
		}
		#endregion

		#region chunk
		public byte[] LoadChunkData(long chunkId, string dataKey)
		{
			ValidateDataKey(dataKey);
			return LoadData(CHUNKS_TABLE, chunkId, dataKey);
		}

		public Task<SQLiteBlob> LoadChunkDataAsync(long chunkId, string dataKey, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return LoadDataAsync(CHUNKS_TABLE, chunkId, dataKey, cancellationToken);
		}

		public void StoreChunkData(long chunkId, string dataKey, byte[] data)
		{
			ValidateDataKey(dataKey);
			StoreData(CHUNKS_TABLE, chunkId, dataKey, data);
		}

		public Task<SQLiteBlob> StoreChunkDataAsync(long chunkId, string dataKey, int dataSize, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return StoreDataAsync(CHUNKS_TABLE, chunkId, dataKey, dataSize, cancellationToken);
		}
		#endregion

		#region map chunk
		public byte[] LoadMapChunkData(long chunkId, string dataKey)
		{
			ValidateDataKey(dataKey);
			return LoadData(MAP_CHUNKS_TABLE, chunkId, dataKey);
		}

		public Task<SQLiteBlob> LoadMapChunkDataAsync(long chunkId, string dataKey, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return LoadDataAsync(MAP_CHUNKS_TABLE, chunkId, dataKey, cancellationToken);
		}

		public void StoreMapChunkData(long chunkId, string dataKey, byte[] data)
		{
			ValidateDataKey(dataKey);
			StoreData(MAP_CHUNKS_TABLE, chunkId, dataKey, data);
		}

		public Task<SQLiteBlob> StoreMapChunkDataAsync(long chunkId, string dataKey, int dataSize, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return StoreDataAsync(MAP_CHUNKS_TABLE, chunkId, dataKey, dataSize, cancellationToken);
		}
		#endregion

		#region map region
		public byte[] LoadMapRegionData(long chunkId, string dataKey)
		{
			ValidateDataKey(dataKey);
			return LoadData(MAP_REGIONS_TABLE, chunkId, dataKey);
		}

		public Task<SQLiteBlob> LoadMapRegionDataAsync(long chunkId, string dataKey, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return LoadDataAsync(MAP_REGIONS_TABLE, chunkId, dataKey, cancellationToken);
		}

		public void StoreMapRegionData(long chunkId, string dataKey, byte[] data)
		{
			ValidateDataKey(dataKey);
			StoreData(MAP_REGIONS_TABLE, chunkId, dataKey, data);
		}

		public Task<SQLiteBlob> StoreMapRegionDataAsync(long chunkId, string dataKey, int dataSize, CancellationToken cancellationToken = default)
		{
			ValidateDataKey(dataKey);
			return StoreDataAsync(MAP_REGIONS_TABLE, chunkId, dataKey, dataSize, cancellationToken);
		}
		#endregion

		#region internal
		private byte[] LoadData(string table, long? position, string dataKey)
		{
			using(var cmd = connection.CreateCommand())
			{
				cmd.CommandText = string.Format(position.HasValue ? GET_POS_DATA_CMD : GET_DATA_CMD, table);
				if(position.HasValue) cmd.Parameters.AddWithValue(POSITION_PARAM, position.Value);
				cmd.Parameters.AddWithValue(KEY_PARAM, dataKey);
				using(var reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
				{
					if(reader.Read())
					{
						return reader.GetValue(0) as byte[];
					}
				}
			}
			return null;
		}

		private async Task<SQLiteBlob> LoadDataAsync(string table, long? position, string dataKey, CancellationToken cancellationToken)
		{
			using(var cmd = connection.CreateCommand())
			{
				cmd.CommandText = string.Format(position.HasValue ? GET_POS_ROWID_CMD : GET_ROWID_CMD, table);
				if(position.HasValue) cmd.Parameters.AddWithValue(POSITION_PARAM, position.Value);
				cmd.Parameters.AddWithValue(KEY_PARAM, dataKey);
				var id = await cmd.ExecuteScalarAsync(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if(id is not long rowId) throw new Exception("An unexpected error occurred");
				return SQLiteBlob.Create(connection, connection.Database, $"{TABLE_PREFIX}_{table}", "data", rowId, true);
			}
		}

		private void StoreData(string table, long? position, string dataKey, byte[] data)
		{
			using(var cmd = connection.CreateCommand())
			{
				cmd.CommandText = string.Format(position.HasValue ? SET_POS_DATA_CMD : SET_DATA_CMD, table);
				if(position.HasValue) cmd.Parameters.AddWithValue(POSITION_PARAM, position.Value);
				cmd.Parameters.AddWithValue(KEY_PARAM, dataKey);
				cmd.Parameters.AddWithValue(DATA_PARAM, data);
				cmd.ExecuteNonQuery();
			}
		}

		private async Task<SQLiteBlob> StoreDataAsync(string table, long? position, string dataKey, int dataSize, CancellationToken cancellationToken)
		{
			using(var cmd = connection.CreateCommand())
			{
				cmd.CommandText = string.Format(position.HasValue ? SET_POS_DATA_ASYNC_CMD : SET_DATA_ASYNC_CMD, table);
				if(position.HasValue) cmd.Parameters.AddWithValue(POSITION_PARAM, position.Value);
				cmd.Parameters.AddWithValue(KEY_PARAM, dataKey);
				cmd.Parameters.AddWithValue(DATA_SIZE_PARAM, dataSize);
				var id = await cmd.ExecuteScalarAsync(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if(id is not long rowId) throw new Exception("An unexpected error occurred");
				return SQLiteBlob.Create(connection, connection.Database, $"{TABLE_PREFIX}_{table}", "data", rowId, false);
			}
		}

		private static unsafe void ValidateDataKey(string dataKey)
		{
			int len = dataKey.Length;
			if(len < 1 || len > 32) throw new ArgumentOutOfRangeException(nameof(dataKey), len, "The data key length must contain at least one character and no more than 32");
			fixed(char* ptr = dataKey)
			{
				var curPtr = ptr;
				for(int i = 0; i < len; i++)
				{
					int c = *curPtr;
					if((c >= 'a' && c <= 'z') || (c >= 'Z' && c <= 'Z') || (c >= '0' && c <= '9') || c == ':' || c == '-' || c == '_')
					{
						curPtr++;
					}
					else
					{
						throw new ArgumentException("The data key can only consist of latin characters, numbers and symbols ':', '-', '_'", nameof(dataKey));
					}
				}
			}
		}
		#endregion
	}
}