using Npgsql;

namespace TurboRamaSuiteOnlineServer;

public static class SuiteDatabasePoolPolicy
{
    public const int DefaultMaximumConnections=8;
    public static string ApplyDefaults(string connection)
    {
        var settings=new NpgsqlConnectionStringBuilder(connection);
        // Leave capacity explicitly assigned by the operator intact. The library
        // default of 100 otherwise consumes all slots of a standard PostgreSQL
        // instance and crowds out admin/content/monitoring during reconnects.
        // Eight also bounds SSI contention on a cold database with two CPUs;
        // adding connections there lengthens transactions and causes retries.
        var supplied=new System.Data.Common.DbConnectionStringBuilder{ConnectionString=connection};
        var explicitMaximum=supplied.Keys.Cast<string>().Any(key=>
            key.Replace(" ","",StringComparison.Ordinal).Equals("MaxPoolSize",StringComparison.OrdinalIgnoreCase)||
            key.Replace(" ","",StringComparison.Ordinal).Equals("MaximumPoolSize",StringComparison.OrdinalIgnoreCase));
        if(!explicitMaximum)settings.MaxPoolSize=Math.Max(DefaultMaximumConnections,settings.MinPoolSize);
        return settings.ConnectionString;
    }
}
