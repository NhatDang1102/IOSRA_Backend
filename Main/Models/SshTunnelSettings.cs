namespace Main.Models
{
    public class SshTunnelSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PrivateKeyPath { get; set; } = string.Empty;
        public uint LocalMySqlPort { get; set; }
        public uint RemoteMySqlPort { get; set; }
        public uint LocalRedisPort { get; set; }
        public uint RemoteRedisPort { get; set; }
    }
}
