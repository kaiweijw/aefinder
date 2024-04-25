using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AeFinder.Block.Dtos;
using AeFinder.BlockSync;
using AeFinder.Entities.Es;
using AeFinder.Etos;
using AElf.EntityMapping.Repositories;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.ObjectMapping;

namespace AeFinder.EntityEventHandler;

public class BlockHandler:IDistributedEventHandler<NewBlocksEto>,
    IDistributedEventHandler<ConfirmBlocksEto>,
    ITransientDependency
{
    private readonly IEntityMappingRepository<BlockIndex, string> _blockIndexRepository;
    private readonly ILogger<BlockHandler> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly IEntityMappingRepository<TransactionIndex, string> _transactionIndexRepository;
    private readonly IEntityMappingRepository<LogEventIndex, string> _logEventIndexRepository;
    private readonly IBlockIndexHandler _blockIndexHandler;
    private readonly IBlockSyncAppService _blockSyncAppService;
    private readonly IEntityMappingRepository<SummaryIndex, string> _summaryIndexRepository;

    public BlockHandler(
        IEntityMappingRepository<BlockIndex, string> blockIndexRepository,
        IEntityMappingRepository<TransactionIndex, string> transactionIndexRepository,
        IEntityMappingRepository<LogEventIndex, string> logEventIndexRepository,
        ILogger<BlockHandler> logger,
        IObjectMapper objectMapper, IBlockIndexHandler blockIndexHandler, IBlockSyncAppService blockSyncAppService,
        IEntityMappingRepository<SummaryIndex, string> summaryIndexRepository)
    {
        _blockIndexRepository = blockIndexRepository;
        _logger = logger;
        _objectMapper = objectMapper;
        _transactionIndexRepository = transactionIndexRepository;
        _logEventIndexRepository = logEventIndexRepository;
        _blockIndexHandler = blockIndexHandler;
        _blockSyncAppService = blockSyncAppService;
        _summaryIndexRepository = summaryIndexRepository;
    }

    public async Task HandleEventAsync(NewBlocksEto eventData)
    {
        _logger.LogInformation(
            $"blocks is adding, start BlockNumber: {eventData.NewBlocks.First().BlockHeight} , Confirmed: {eventData.NewBlocks.First().Confirmed}, end BlockNumber: {eventData.NewBlocks.Last().BlockHeight}");

        try
        {
            var blockIndexList = new List<BlockIndex>();
            foreach (var newBlock in eventData.NewBlocks)
            {
                var blockIndex = _objectMapper.Map<NewBlockEto, BlockIndex>(newBlock);
                blockIndex.TransactionIds = newBlock.Transactions.Select(b => b.TransactionId).ToList();
                blockIndex.LogEventCount = newBlock.Transactions.Sum(t => t.LogEvents.Count);
                blockIndexList.Add(blockIndex);
            }

            var tasks = new List<Task>
            {
                _blockIndexRepository.AddOrUpdateManyAsync(blockIndexList)
            };

            List<TransactionIndex> transactionIndexList = new List<TransactionIndex>();
            List<LogEventIndex> logEventIndexList = new List<LogEventIndex>();

            foreach (var newBlock in eventData.NewBlocks)
            {
                foreach (var transaction in newBlock.Transactions)
                {
                    var transactionIndex = _objectMapper.Map<Transaction, TransactionIndex>(transaction);
                    transactionIndexList.Add(transactionIndex);

                    foreach (var logEvent in transaction.LogEvents)
                    {
                        var logEventIndex = _objectMapper.Map<LogEvent, LogEventIndex>(logEvent);
                        logEventIndexList.Add(logEventIndex);
                    }
                }
            }

            if (transactionIndexList.Count > 0)
            {
                tasks.Add(_transactionIndexRepository.AddOrUpdateManyAsync(transactionIndexList));
            }

            if (logEventIndexList.Count > 0)
            {
                tasks.Add( _logEventIndexRepository.AddOrUpdateManyAsync(logEventIndexList));
            }
            
            //Record latest new block height
            var chainId = eventData.NewBlocks.Last().ChainId;
            var summaryIndex = await _summaryIndexRepository.GetAsync(chainId);
            if (summaryIndex == null)
            {
                summaryIndex = new SummaryIndex();
                summaryIndex.ChainId = chainId;
            }
            summaryIndex.LatestBlockHeight = eventData.NewBlocks.Last().BlockHeight;
            tasks.Add( _summaryIndexRepository.AddOrUpdateAsync(summaryIndex));

            await tasks.WhenAll();
            
            var blockDtos = _objectMapper.Map<List<NewBlockEto>, List<BlockWithTransactionDto>>(eventData.NewBlocks);

            _ = Task.Run(async () =>
            {
                foreach (var dto in blockDtos)
                {
                    await _blockIndexHandler.ProcessNewBlockAsync(dto);
                }
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                $"Handle newBlocks add event error:{e.Message}，start BlockHeight: {eventData.NewBlocks.First().BlockHeight}, end BlockHeight: {eventData.NewBlocks.Last().BlockHeight},retrying...");
            await HandleEventAsync(eventData);
        }
    }

    public async Task HandleEventAsync(ConfirmBlocksEto eventData)
    {
        List<BlockIndex> confirmBlockIndexList = new List<BlockIndex>();
        List<TransactionIndex> confirmTransactionIndexList = new List<TransactionIndex>();
        List<LogEventIndex> confirmLogEventIndexList = new List<LogEventIndex>(); 
        var indexes = new List<BlockIndex>();
        var lastBlock = eventData.ConfirmBlocks.Last();
        var syncMode = await _blockSyncAppService.GetBlockSyncModeAsync(lastBlock.ChainId, lastBlock.BlockHeight);
        _logger.LogInformation("block:{FirstBlockHeight}-{LastBlockHeight} is confirming. sync mode: {SyncMode}.",
            eventData.ConfirmBlocks.First().BlockHeight, lastBlock.BlockHeight, syncMode);
        foreach (var confirmBlock in eventData.ConfirmBlocks)
        {
            var blockIndex = _objectMapper.Map<ConfirmBlockEto, BlockIndex>(confirmBlock);
            blockIndex.TransactionIds = confirmBlock.Transactions.Select(b => b.TransactionId).ToList();
            
            // _logger.LogInformation("confirm block transacionids count:" + blockIndex.TransactionIds.Count);
            blockIndex.LogEventCount = confirmBlock.Transactions.Sum(t => t.LogEvents.Count);
            blockIndex.Confirmed = true;

            confirmBlockIndexList.Add(blockIndex);
            indexes.Add(blockIndex);

            foreach (var transaction in confirmBlock.Transactions)
            {
                var transactionIndex = _objectMapper.Map<Transaction, TransactionIndex>(transaction);
                transactionIndex.Confirmed = true;
                foreach (var logEvent in transactionIndex.LogEvents)
                {
                    logEvent.Confirmed = true;
                }
                confirmTransactionIndexList.Add(transactionIndex);

                foreach (var logEvent in transaction.LogEvents)
                {
                    var logEventIndex = _objectMapper.Map<LogEvent, LogEventIndex>(logEvent);
                    logEventIndex.Confirmed = true;
                    confirmLogEventIndexList.Add(logEventIndex);
                }
            }

            if (syncMode == BlockSyncMode.FastSyncMode)
            {
                continue;
            }

            //find the same height blocks
            var queryable = await _blockIndexRepository.GetQueryableAsync();
            Expression<Func<BlockIndex, bool>> expression = p => p.ChainId == confirmBlock.ChainId && p.BlockHeight == confirmBlock.BlockHeight;
            var forkBlockList = queryable.Where(expression).ToList();
            if (forkBlockList.Count == 0)
            {
                continue;
            }

            //delete the same height fork block
            List<BlockIndex> forkBlockIndexList = new List<BlockIndex>();
            List<TransactionIndex> forkTransactionIndexList = new List<TransactionIndex>();
            List<LogEventIndex> forkLogEventIndexList = new List<LogEventIndex>();
            foreach (var forkBlock in forkBlockList)
            {
                if (forkBlock.BlockHash == confirmBlock.BlockHash)
                {
                    continue;
                }

                forkBlockIndexList.Add(forkBlock);

                var transactionIndexList = await GetTransactionListAsync(forkBlock.ChainId,forkBlock.BlockHash);
                forkTransactionIndexList.AddRange(transactionIndexList);

                foreach (var transaction in transactionIndexList)
                {
                    foreach (var logEvent in transaction.LogEvents)
                    {
                        var logEventIndex = _objectMapper.Map<LogEvent, LogEventIndex>(logEvent);
                        forkLogEventIndexList.Add(logEventIndex);
                    }
                }
            }

            if (forkBlockIndexList.Count > 0)
            {
                _logger.LogDebug($"bulk delete blocks,total count {forkBlockIndexList.Count}");
                await _blockIndexRepository.DeleteManyAsync(forkBlockIndexList);
            }
            if (forkTransactionIndexList.Count > 0)
            {
                _logger.LogDebug($"bulk delete transactions,total count {forkTransactionIndexList.Count}");
                await _transactionIndexRepository.DeleteManyAsync(forkTransactionIndexList);
            }
            if (forkLogEventIndexList.Count > 0)
            {
                _logger.LogDebug($"bulk delete log events,total count {forkLogEventIndexList.Count}");
                await _logEventIndexRepository.DeleteManyAsync(forkLogEventIndexList);
            }
        }

        _logger.LogDebug("blocks is confirming,start {FirstBlockHeight} end {LastBlockHeight},total confirm {Count}",
            confirmBlockIndexList.First().BlockHeight, confirmBlockIndexList.Last().BlockHeight,
            confirmBlockIndexList.Count);
        var tasks = new List<Task>
        {
            _blockIndexRepository.AddOrUpdateManyAsync(confirmBlockIndexList)
        };
        if (confirmTransactionIndexList.Count > 0)
        {
            tasks.Add(_transactionIndexRepository.AddOrUpdateManyAsync(confirmTransactionIndexList));
        }
        if (confirmLogEventIndexList.Count > 0)
        {
            tasks.Add(_logEventIndexRepository.AddOrUpdateManyAsync(confirmLogEventIndexList));
        }
        
        //Record latest confirmed block height
        var chainId = lastBlock.ChainId;
        var summaryIndex = await _summaryIndexRepository.GetAsync(chainId);
        if (summaryIndex == null)
        {
            summaryIndex = new SummaryIndex();
            summaryIndex.ChainId = chainId;
        }
        summaryIndex.ConfirmedBlockHeight = lastBlock.BlockHeight;
        tasks.Add( _summaryIndexRepository.AddOrUpdateAsync(summaryIndex));
        
        await tasks.WhenAll();
        
        var blockDtos = _objectMapper.Map<List<ConfirmBlockEto>, List<BlockWithTransactionDto>>(eventData.ConfirmBlocks);
        _ = Task.Run(async () =>
        {
            foreach (var dto in blockDtos)
            {
                if (syncMode == BlockSyncMode.FastSyncMode)
                {
                    await _blockIndexHandler.ProcessNewBlockAsync(dto);
                }

                await _blockIndexHandler.ProcessConfirmedBlocksAsync(dto);
            }
        });
    }

    private async Task<List<TransactionIndex>> GetTransactionListAsync(string chainId,string blockHash)
    {
        Expression<Func<TransactionIndex, bool>> expression = p => p.ChainId == chainId && p.BlockHash == blockHash;
        var queryable = await _transactionIndexRepository.GetQueryableAsync();
        var forkTransactionList =  queryable.Where(expression).ToList();
        if (forkTransactionList.Count == 0)
        {
            return new List<TransactionIndex>();
        }

        return forkTransactionList;
    }


}