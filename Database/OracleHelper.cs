using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DbAutoInsert.Database
{
    public sealed class OracleHelper : IDisposable
    {
        public string ConnectionString { get; private set; } = null;

        const string MSG_NULL_COMMAND      = "Command가 NULL입니다.";
        const string MSG_EMPTY_COMMANDTEXT = "CommandText가 제공되지 않았습니다.";
        const string MSG_NULL_TRANSACTION  = "Transaction이 NULL입니다.";
        const string MSG_CLOSE_TRANSACTION = "Transaction이 롤백되었거나 커밋 되었습니다.";

        public OracleHelper(string userId, string password, string tnsName)
            : this($"User Id={userId}; Password={password}; Data Source={tnsName};") { }

        public OracleHelper(string connectionString)
        {
            using (var conn = new OracleConnection(connectionString))
            {
                conn.Open();
                conn.Close();
                ConnectionString = connectionString;
            }
        }

        ~OracleHelper() { Dispose(); }

        public void Dispose() { ConnectionString = null; }

        public OracleConnection GetOracleConnection() => CreateConnection();

        public OracleTransaction GetOracleTransaction()
        {
            var conn = CreateConnection();
            if (conn.State != ConnectionState.Open) conn.Open();
            return conn.BeginTransaction();
        }

        private OracleConnection CreateConnection()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString)) return null;
            return new OracleConnection(ConnectionString);
        }

        private void PrepareCommand(OracleCommand cmd, OracleTransaction tx, CommandType type,
            string text, IEnumerable<OracleParameter> prms, out bool mustClose)
        {
            if (cmd == null) throw new ArgumentNullException(MSG_NULL_COMMAND);
            if (string.IsNullOrEmpty(text)) throw new ArgumentNullException(MSG_EMPTY_COMMANDTEXT);

            OracleConnection conn;
            if (tx != null && tx.Connection != null)      conn = tx.Connection;
            else if (tx != null && tx.Connection == null) throw new ArgumentException(MSG_CLOSE_TRANSACTION);
            else                                          conn = CreateConnection();

            mustClose = conn.State != ConnectionState.Open;
            if (mustClose) conn.Open();

            cmd.Connection  = conn;
            cmd.CommandText = text;
            cmd.CommandType = type;
            if (tx != null) cmd.Transaction = tx;
            if (prms != null)
            {
                cmd.BindByName = false;
                foreach (var p in prms.Where(p => p != null))
                {
                    if ((p.Direction == ParameterDirection.InputOutput ||
                         p.Direction == ParameterDirection.Input) && p.Value == null)
                        p.Value = DBNull.Value;
                    cmd.Parameters.Add(p);
                }
            }
        }

        public int ExecuteNonQuery(string sql) => ExecuteNonQuery(CommandType.Text, sql, (OracleParameter[])null);
        public int ExecuteNonQuery(string sql, params OracleParameter[] p) => ExecuteNonQuery(CommandType.Text, sql, p);
        public int ExecuteNonQuery(CommandType type, string sql, params OracleParameter[] p)
        {
            var cmd = new OracleCommand();
            PrepareCommand(cmd, null, type, sql, p, out var close);
            var r = cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
            if (close) cmd.Connection.Close();
            return r;
        }
        public int ExecuteNonQuery(OracleTransaction tx, string sql) => ExecuteNonQuery(tx, CommandType.Text, sql, (OracleParameter[])null);
        public int ExecuteNonQuery(OracleTransaction tx, string sql, params OracleParameter[] p) => ExecuteNonQuery(tx, CommandType.Text, sql, p);
        public int ExecuteNonQuery(OracleTransaction tx, CommandType type, string sql, params OracleParameter[] p)
        {
            if (tx == null) throw new ArgumentNullException(MSG_NULL_TRANSACTION);
            if (tx.Connection == null) throw new ArgumentException(MSG_CLOSE_TRANSACTION);
            var cmd = new OracleCommand();
            PrepareCommand(cmd, tx, type, sql, p, out _);
            var r = cmd.ExecuteNonQuery();
            cmd.Parameters.Clear();
            return r;
        }

        public object ExecuteScalar(string sql) => ExecuteScalar(CommandType.Text, sql, (OracleParameter[])null);
        public object ExecuteScalar(string sql, params OracleParameter[] p) => ExecuteScalar(CommandType.Text, sql, p);
        public object ExecuteScalar(CommandType type, string sql, params OracleParameter[] p)
        {
            var cmd = new OracleCommand();
            PrepareCommand(cmd, null, type, sql, p, out var close);
            var r = cmd.ExecuteScalar();
            cmd.Parameters.Clear();
            if (close) cmd.Connection.Close();
            return r;
        }
        public object ExecuteScalar(OracleTransaction tx, string sql, params OracleParameter[] p)
        {
            if (tx == null) throw new ArgumentNullException(MSG_NULL_TRANSACTION);
            if (tx.Connection == null) throw new ArgumentException(MSG_CLOSE_TRANSACTION);
            var cmd = new OracleCommand();
            PrepareCommand(cmd, tx, CommandType.Text, sql, p, out _);
            var r = cmd.ExecuteScalar();
            cmd.Parameters.Clear();
            return r;
        }

        public DataTable ExecuteDataTable(string sql) => ExecuteDataSet(sql).Tables[0];
        public DataTable ExecuteDataTable(string sql, params OracleParameter[] p) => ExecuteDataSet(sql, p).Tables[0];
        public DataSet ExecuteDataSet(string sql) => ExecuteDataSet(CommandType.Text, sql, (OracleParameter[])null);
        public DataSet ExecuteDataSet(string sql, params OracleParameter[] p) => ExecuteDataSet(CommandType.Text, sql, p);
        public DataSet ExecuteDataSet(CommandType type, string sql, params OracleParameter[] p)
        {
            var cmd = new OracleCommand();
            var ds  = new DataSet();
            PrepareCommand(cmd, null, type, sql, p, out var close);
            using (var da = new OracleDataAdapter(cmd)) da.Fill(ds);
            cmd.Parameters.Clear();
            if (close) cmd.Connection.Close();
            return ds;
        }

        public OracleDataReader ExecuteReader(string sql) => ExecuteReader(CommandType.Text, sql, (OracleParameter[])null);
        public OracleDataReader ExecuteReader(string sql, params OracleParameter[] p) => ExecuteReader(CommandType.Text, sql, p);
        public OracleDataReader ExecuteReader(CommandType type, string sql, params OracleParameter[] p)
        {
            var cmd = new OracleCommand();
            PrepareCommand(cmd, null, type, sql, p, out var mustClose);
            return cmd.ExecuteReader(mustClose ? CommandBehavior.CloseConnection : CommandBehavior.Default);
        }
    }
}
