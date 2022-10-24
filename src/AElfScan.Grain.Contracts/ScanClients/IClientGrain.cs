using Orleans;

namespace AElfScan.Grain.Contracts.ScanClients;

public interface IClientGrain : IGrainWithStringKey
{
    Task<ClientInfo> GetClientInfoAsync();
    Task<SubscribeInfo> GetSubscribeInfoAsync();
    Task SetScanNewBlockStartHeightAsync(long height);
    Task<string> InitializeAsync(string chainId, string clientId, SubscribeInfo info);
}