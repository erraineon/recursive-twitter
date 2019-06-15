namespace RecursiveTwitter
{
    internal class Snowflake
    {
        public Snowflake(ulong id)
        {
            Sequence = (uint) (id & 0xfff);
            WorkerId = (uint) ((id & 0x1f000) >> 12);
            DatacenterId = (uint) ((id & 0x3e0000) >> 17);
            Timestamp = id >> 22;
        }

        public Snowflake(uint sequence, uint datacenterId, uint workerId, ulong timestamp)
        {
            Sequence = sequence;
            DatacenterId = datacenterId;
            WorkerId = workerId;
            Timestamp = timestamp;
        }

        public ulong Timestamp { get; }

        public uint DatacenterId { get; }

        public uint WorkerId { get; }

        public uint Sequence { get; }

        public ulong Id => (Timestamp << 22) + (DatacenterId << 17) + (WorkerId << 12) + Sequence;
    }
}