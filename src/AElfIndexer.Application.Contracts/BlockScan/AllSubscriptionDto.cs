using System.Collections.Generic;

namespace AElfIndexer.BlockScan;

public class AllSubscriptionDto
{
    public AllSubscriptionDetailDto CurrentVersion { get; set; }
    
    public AllSubscriptionDetailDto NewVersion { get; set; }
}

public class AllSubscriptionDetailDto
{
    public string Version { get; set; }
    public SubscriptionStatus Status { get; set; }
    public SubscriptionDto Subscription { get; set; }
}