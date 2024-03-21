using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using AeFinder.Grains;
using AeFinder.Grains.Grain.BlockPush;
using AeFinder.Grains.Grain.BlockStates;
using AeFinder.Grains.Grain.Subscriptions;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp;
using Volo.Abp.Auditing;

namespace AeFinder.BlockScan;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class BlockScanAppService : AeFinderAppService, IBlockScanAppService
{
    private readonly IClusterClient _clusterClient;

    public BlockScanAppService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<string> AddSubscriptionAsync(string clientId, SubscriptionManifestDto manifestDto)
    {
        Logger.LogInformation("ScanApp: {clientId} submit subscription: {subscription}", clientId,
            JsonSerializer.Serialize(manifestDto));

        var subscription = ObjectMapper.Map<SubscriptionManifestDto,SubscriptionManifest>(manifestDto);
        var client = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        // TODO: Use the code from developer.
        var version = await client.AddSubscriptionAsync(subscription, new byte[]{});
        return version;
    }
    
    // public async Task UpdateSubscriptionInfoAsync(string clientId, string version, List<SubscriptionInfo> subscriptionInfos)
    // {
    //     Logger.LogInformation($"Client: {clientId} update subscribe: {JsonSerializer.Serialize(subscriptionInfos)}");
    //     
    //     var client = _clusterClient.GetGrain<IClientGrain>(clientId);
    //     
    //     //Check if the subscription info is valid
    //     var currentSubscriptionInfos = await client.GetSubscriptionAsync(version);
    //
    //     CheckInputSubscriptionInfoIsValid(subscriptionInfos, currentSubscriptionInfos);
    //
    //     CheckInputSubscriptionInfoIsDuplicateOrMissing(subscriptionInfos, currentSubscriptionInfos);
    //     
    //     //Update subscription info
    //     await client.UpdateSubscriptionInfoAsync(version, subscriptionInfos);
    //
    //     foreach (var subscriptionInfo in subscriptionInfos)
    //     {
    //         var id = GrainIdHelper.GenerateGrainId(subscriptionInfo.ChainId, clientId, version, subscriptionInfo.FilterType);
    //         var blockScanInfoGrain = _clusterClient.GetGrain<IBlockScanGrain>(id);
    //         await blockScanInfoGrain.UpdateSubscriptionInfoAsync(subscriptionInfo);
    //     }
    // }

    // private void CheckInputSubscriptionInfoIsValid(List<SubscriptionInfo> subscriptionInfos,
    //     List<SubscriptionInfo> currentSubscriptionInfos)
    // {
    //     foreach (var subscriptionInfo in subscriptionInfos)
    //     {
    //         var subscriptionInfoForCheckChainId = currentSubscriptionInfos.FindAll(i =>
    //             (i.ChainId == subscriptionInfo.ChainId));
    //         if (subscriptionInfoForCheckChainId == null || subscriptionInfoForCheckChainId.Count == 0)
    //         {
    //             var errorMessage = $"Invalid chain id {subscriptionInfo.ChainId}, can not add new chain";
    //             throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //         }
    //
    //         var subscriptionInfoForCheckFilterType = subscriptionInfoForCheckChainId.FindAll(i =>
    //             i.FilterType == subscriptionInfo.FilterType);
    //         if (subscriptionInfoForCheckFilterType == null || subscriptionInfoForCheckFilterType.Count == 0)
    //         {
    //             continue;
    //         }
    //
    //         var subscriptionInfoForCheckStartBlockNumber = subscriptionInfoForCheckFilterType.FindAll(i =>
    //             i.StartBlockNumber == subscriptionInfo.StartBlockNumber);
    //         if (subscriptionInfoForCheckStartBlockNumber == null || subscriptionInfoForCheckStartBlockNumber.Count == 0)
    //         {
    //             var errorMessage =
    //                 $"Invalid start block number {subscriptionInfo.StartBlockNumber}, can not update start block number in chain {subscriptionInfo.ChainId} filterType {subscriptionInfo.FilterType}";
    //             throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //         }
    //
    //         var subscriptionInfoForCheckIsOnlyConfirmed = subscriptionInfoForCheckStartBlockNumber.FindAll(i =>
    //             i.OnlyConfirmedBlock == subscriptionInfo.OnlyConfirmedBlock);
    //         if (subscriptionInfoForCheckIsOnlyConfirmed == null || subscriptionInfoForCheckIsOnlyConfirmed.Count == 0)
    //         {
    //             var errorMessage =
    //                 $"Invalid only confirmed block {subscriptionInfo.OnlyConfirmedBlock}, can not update only confirmed block in chain {subscriptionInfo.ChainId} filterType {subscriptionInfo.FilterType}";
    //             throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //         }
    //
    //         var subscriptionInfoForCheckContract = subscriptionInfoForCheckIsOnlyConfirmed.FirstOrDefault();
    //         if (subscriptionInfo.SubscribeEvents == null || subscriptionInfo.SubscribeEvents.Count == 0)
    //         {
    //             if (subscriptionInfoForCheckContract.SubscribeEvents != null &&
    //                 subscriptionInfoForCheckContract.SubscribeEvents.Count > 0)
    //             {
    //                 var errorMessage =
    //                     $"Can not empty subscribe contracts in chain {subscriptionInfo.ChainId} filterType {subscriptionInfo.FilterType}";
    //                 throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //             }
    //         }
    //         else
    //         {
    //             if (subscriptionInfoForCheckContract.SubscribeEvents == null ||
    //                 subscriptionInfoForCheckContract.SubscribeEvents.Count == 0)
    //             {
    //                 var errorMessage =
    //                     $"Can not add subscribe contracts in chain {subscriptionInfo.ChainId} filterType {subscriptionInfo.FilterType}";
    //                 throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //             }
    //
    //             foreach (var filterContractEventInput in subscriptionInfo.SubscribeEvents)
    //             {
    //                 var subscriptionInfoForCheckContractEvent =
    //                     subscriptionInfoForCheckContract.SubscribeEvents.FirstOrDefault(i =>
    //                         (i.ContractAddress == filterContractEventInput.ContractAddress));
    //                 if (subscriptionInfoForCheckContractEvent == null)
    //                 {
    //                     var errorMessage =
    //                         $"Can not add new subscribe contract {filterContractEventInput.ContractAddress} in chain {subscriptionInfo.ChainId} filterType {subscriptionInfo.FilterType}";
    //                     throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //                 }
    //             }
    //         }
    //     }
    // }
    //
    // private void CheckInputSubscriptionInfoIsDuplicateOrMissing(List<SubscriptionInfo> subscriptionInfos,
    //     List<SubscriptionInfo> currentSubscriptionInfos)
    // {
    //     foreach (var currentSubscriptionInfo in currentSubscriptionInfos)
    //     {
    //         var subscriptionInfoForCheckDuplicate= subscriptionInfos.FindAll(i =>
    //             (i.ChainId == currentSubscriptionInfo.ChainId && i.FilterType == currentSubscriptionInfo.FilterType));
    //         if (subscriptionInfoForCheckDuplicate != null && subscriptionInfoForCheckDuplicate.Count > 1)
    //         {
    //             var errorMessage =
    //                 $"Duplicate subscribe information in chain {currentSubscriptionInfo.ChainId} filterType {currentSubscriptionInfo.FilterType}";
    //             throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //         }
    //         
    //         var subscriptionInfoForCheck = subscriptionInfoForCheckDuplicate.FirstOrDefault(i =>
    //             (i.StartBlockNumber == currentSubscriptionInfo.StartBlockNumber && i.OnlyConfirmedBlock == currentSubscriptionInfo.OnlyConfirmedBlock));
    //         if (subscriptionInfoForCheck == null)
    //         {
    //             var errorMessage =
    //                 $"Missing subscribe information in chain {currentSubscriptionInfo.ChainId} filterType {currentSubscriptionInfo.FilterType}";
    //             throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //         }
    //
    //         if (currentSubscriptionInfo.SubscribeEvents != null && currentSubscriptionInfo.SubscribeEvents.Count > 0)
    //         {
    //             foreach (var currentSubscribeContract in currentSubscriptionInfo.SubscribeEvents)
    //             {
    //                 var subscriptionInfoForCheckContractEvent = subscriptionInfoForCheck.SubscribeEvents.FirstOrDefault(i =>
    //                     (i.ContractAddress == currentSubscribeContract.ContractAddress));
    //                 if (subscriptionInfoForCheckContractEvent == null)
    //                 {
    //                     var errorMessage =
    //                         $"Can not remove subscribe contract {currentSubscribeContract.ContractAddress} in chain {currentSubscriptionInfo.ChainId} filterType {currentSubscriptionInfo.FilterType}";
    //                     throw new UserFriendlyException("Invalid subscriptionInfo", details: errorMessage);
    //                 }
    //             }
    //         }
    //     }
    // }
    
    public async Task<List<Guid>> GetMessageStreamIdsAsync(string clientId, string version)
    {
        var client = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        var subscription = await client.GetSubscriptionAsync(version);
        var streamIds = new List<Guid>();
        foreach (var subscriptionItem in subscription.SubscriptionItems)
        {
            var id = GrainIdHelper.GenerateBlockPusherGrainId(clientId, version, subscriptionItem.ChainId);
            var blockScanInfoGrain = _clusterClient.GetGrain<IBlockPusherInfoGrain>(id);
            var streamId = await blockScanInfoGrain.GetMessageStreamIdAsync();
            streamIds.Add(streamId);
        }

        return streamIds;
    }

    public async Task StartScanAsync(string clientId, string version)
    {
        Logger.LogInformation("ScanApp: {clientId} start scan, version: {version}", clientId, version);

        var client = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        var subscription = await client.GetSubscriptionAsync(version);
        var versionStatus = await client.GetSubscriptionStatusAsync(version);
        var scanToken = Guid.NewGuid().ToString("N");
        await client.StartAsync(version);
        foreach (var subscriptionItem in subscription.SubscriptionItems)
        {
            var id = GrainIdHelper.GenerateBlockPusherGrainId(clientId, version, subscriptionItem.ChainId);
            var blockScanGrain = _clusterClient.GetGrain<IBlockPusherInfoGrain>(id);
            var blockScanExecutorGrain = _clusterClient.GetGrain<IBlockPusherGrain>(id);

            var startBlockHeight = subscriptionItem.StartBlockNumber;
            
            if (versionStatus != SubscriptionStatus.Initialized)
            {
                var appBlockStateSetStatusGrain = _clusterClient.GetGrain<IAppBlockStateSetStatusGrain>(
                    GrainIdHelper.GenerateAppBlockStateSetStatusGrainId(clientId, version, subscriptionItem.ChainId));
                // TODO: This is not correct, we should get the confirmed block height from the chain grain.
                startBlockHeight = (await appBlockStateSetStatusGrain.GetBlockStateSetStatusAsync()).LastIrreversibleBlockHeight;
                if (startBlockHeight == 0)
                {
                    startBlockHeight = subscriptionItem.StartBlockNumber;
                }
                else
                {
                    startBlockHeight += 1;
                }
            }
            
            await blockScanGrain.InitializeAsync(clientId, version, subscriptionItem, scanToken);
            await blockScanExecutorGrain.InitializeAsync(scanToken, startBlockHeight);

            Logger.LogDebug("Start ScanApp: {clientId}, id: {id}", clientId, id);
            _ = Task.Run(blockScanExecutorGrain.HandleHistoricalBlockAsync);
        }
    }

    public async Task UpgradeVersionAsync(string clientId)
    {
        var client = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        await client.UpgradeVersionAsync();
    }

    public async Task<AllSubscriptionDto> GetSubscriptionAsync(string clientId)
    {
        var clientGrain = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        var allSubscription = await clientGrain.GetAllSubscriptionAsync();
        return ObjectMapper.Map<AllSubscription, AllSubscriptionDto>(allSubscription);
    }

    public async Task PauseAsync(string clientId, string version)
    {
        var client = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        var scanManager = _clusterClient.GetGrain<IBlockPusherManagerGrain>(0);
        var subscription = await client.GetSubscriptionAsync(version);
        await client.PauseAsync(version);
        foreach (var subscriptionItem in subscription.SubscriptionItems)
        {
            var id = GrainIdHelper.GenerateBlockPusherGrainId(clientId, version, subscriptionItem.ChainId);
            await scanManager.RemoveBlockPusherAsync(subscriptionItem.ChainId, id);
        }
    }

    public async Task StopAsync(string clientId, string version)
    {
        var clientGrain = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        await clientGrain.StopAsync(version);
    }
    
    public async Task<bool> IsRunningAsync(string chainId, string clientId, string version, string token)
    {
        var clientGrain = _clusterClient.GetGrain<IAppSubscriptionGrain>(GrainIdHelper.GenerateAppGrainId(clientId));
        return await clientGrain.IsRunningAsync(version, chainId, token);
    }
}