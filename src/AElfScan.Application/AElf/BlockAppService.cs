using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using AElfScan.AElf.Dtos;
using AElfScan.AElf.Entities.Es;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace AElfScan.AElf;

[RemoteService(IsEnabled = false)]
public class BlockAppService:ApplicationService,IBlockAppService
{
    private readonly INESTRepository<Block, string> _blockIndexRepository;
    private readonly ApiOptions _apiOptions;
    
    public BlockAppService(INESTRepository<Block, string> blockIndexRepository,
        IOptionsSnapshot<ApiOptions> apiOptions)
    {
        _blockIndexRepository = blockIndexRepository;
        _apiOptions = apiOptions.Value;
    }

    public async Task<List<BlockDto>> GetBlocksAsync(GetBlocksInput input)
    {
        if (input.EndBlockNumber - input.StartBlockNumber > _apiOptions.BlockQueryAmountInterval)
        {
            input.EndBlockNumber = input.StartBlockNumber + _apiOptions.BlockQueryAmountInterval;
        }
        
        var mustQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
        mustQuery.Add(q => q.Range(i => i.Field(f => f.BlockNumber).GreaterThanOrEquals(input.StartBlockNumber)));
        mustQuery.Add(q => q.Range(i => i.Field(f => f.BlockNumber).LessThanOrEquals(input.EndBlockNumber)));
        
        QueryContainer Filter(QueryContainerDescriptor<Block> f) => f.Bool(b => b.Must(mustQuery));

        var list = await _blockIndexRepository.GetListAsync(Filter);

        var items = ObjectMapper.Map<List<Block>, List<BlockDto>>(list.Item2);
        
        List<BlockDto> resultList = new List<BlockDto>();
        if (!input.HasTransaction)
        {
            foreach (var blockItem in items)
            {
                blockItem.Transactions = null;
                resultList.Add(blockItem);
            }
        }
        else
        {
            resultList.AddRange(items);
        }
        
        return resultList;
    }

    public async Task<List<TransactionDto>> GetTransactionsAsync(GetTransactionsInput input)
    {
        if (input.EndBlockNumber - input.StartBlockNumber > _apiOptions.BlockQueryAmountInterval)
        {
            input.EndBlockNumber = input.StartBlockNumber + _apiOptions.BlockQueryAmountInterval;
        }
        
        var mustQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
        mustQuery.Add(q => q.Range(i => i.Field("Transactions.blockNumber").GreaterThanOrEquals(input.StartBlockNumber)));
        mustQuery.Add(q => q.Range(i => i.Field("Transactions.blockNumber").LessThanOrEquals(input.EndBlockNumber)));
        
        if (input.Contracts!=null)
        {
            var shouldQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
            foreach (ContractInput contractInput in input.Contracts)
            {
                var shouldMustQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
                if (!string.IsNullOrEmpty(contractInput.ContractAddress))
                {
                    shouldMustQuery.Add(s =>
                        s.Match(i=>i.Field("Transactions.LogEvents.contractAddress").Query(contractInput.ContractAddress)));
                }

                var shouldMushShouldQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
                foreach (var eventName in contractInput.EventNames)
                {
                    if (!string.IsNullOrEmpty(eventName))
                    {
                        shouldMushShouldQuery.Add(s =>
                            s.Match(i => i.Field("Transactions.LogEvents.eventName").Query(eventName)));
                    }
                }

                if (shouldMushShouldQuery.Count > 0)
                {
                    shouldMustQuery.Add(q=>q.Bool(b=>b.Should(shouldMushShouldQuery)));
                }

                shouldQuery.Add(q=>q.Bool(b=>b.Must(shouldMustQuery)));
            }
            mustQuery.Add(q=>q.Bool(b=>b.Should(shouldQuery)));
        }
        
        
        QueryContainer Filter(QueryContainerDescriptor<Block> f) => f.Nested(q => q.Path("Transactions")
            .Query(qq => qq.Bool(b => b.Must(mustQuery))));
        // QueryContainer Filter(QueryContainerDescriptor<Block> f) => f.Nested(q => q.Path("Transactions")
        //     .Query(qq => qq.Bool(b => b.Must(s =>
        //         s.Match(i=>i.Field("Transactions.LogEvents.eventName").Query("IrreversibleBlockFound"))))));
        List<TransactionDto> resultList = new List<TransactionDto>();

        try
        {
            var list = await _blockIndexRepository.GetListAsync(Filter);

            var items = ObjectMapper.Map<List<Block>, List<BlockDto>>(list.Item2);

            var contractAddressList = input.Contracts.Select(i => i.ContractAddress);
            foreach (var blockItem in items)
            {
                foreach (var transactionItem in blockItem.Transactions)
                {
                    bool isWantedTransaction = false;
                    foreach (var logEventItem in transactionItem.LogEvents)
                    {
                        foreach (var contractInputItem in input.Contracts)
                        {
                            if (!string.IsNullOrEmpty(contractInputItem.ContractAddress) && 
                                contractInputItem.EventNames.Count>0)
                            {
                                if (contractInputItem.ContractAddress==logEventItem.ContractAddress
                                    && contractInputItem.EventNames.Contains(logEventItem.EventName))
                                {
                                    isWantedTransaction = true;
                                }
                            }
                            else if (!string.IsNullOrEmpty(contractInputItem.ContractAddress) &&
                                     contractInputItem.EventNames.Count <= 0)
                            {
                                if (contractInputItem.ContractAddress==logEventItem.ContractAddress)
                                {
                                    isWantedTransaction = true;
                                }
                            }
                            else
                            {
                                if (contractInputItem.EventNames.Contains(logEventItem.EventName))
                                {
                                    isWantedTransaction = true;
                                }
                            }
                        }
                    }

                    if (isWantedTransaction)
                    {
                        resultList.Add(transactionItem);
                    }
                }
            }
            
            if (!input.HasLogEvent)
            {
                foreach (var blockItem in resultList)
                {
                    blockItem.LogEvents = null;
                }
            }

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw e;
        }

        return resultList;
    }
    
    public async Task<List<LogEventDto>> GetLogEventsAsync(GetLogEventsInput input)
    {
        if (input.EndBlockNumber - input.StartBlockNumber > _apiOptions.BlockQueryAmountInterval)
        {
            input.EndBlockNumber = input.StartBlockNumber + _apiOptions.BlockQueryAmountInterval;
        }
        
        var mustQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
        mustQuery.Add(q => q.Range(i => i.Field("Transactions.blockNumber").GreaterThanOrEquals(input.StartBlockNumber)));
        mustQuery.Add(q => q.Range(i => i.Field("Transactions.blockNumber").LessThanOrEquals(input.EndBlockNumber)));
        
        if (input.Contracts!=null)
        {
            var shouldQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
            foreach (ContractInput contractInput in input.Contracts)
            {
                var shouldMustQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
                if (!string.IsNullOrEmpty(contractInput.ContractAddress))
                {
                    shouldMustQuery.Add(s =>
                        s.Match(i=>i.Field("Transactions.LogEvents.contractAddress").Query(contractInput.ContractAddress)));
                }

                var shouldMushShouldQuery = new List<Func<QueryContainerDescriptor<Block>, QueryContainer>>();
                foreach (var eventName in contractInput.EventNames)
                {
                    if (!string.IsNullOrEmpty(eventName))
                    {
                        shouldMushShouldQuery.Add(s =>
                            s.Match(i => i.Field("Transactions.LogEvents.eventName").Query(eventName)));
                    }
                }

                if (shouldMushShouldQuery.Count > 0)
                {
                    shouldMustQuery.Add(q=>q.Bool(b=>b.Should(shouldMushShouldQuery)));
                }

                shouldQuery.Add(q=>q.Bool(b=>b.Must(shouldMustQuery)));
            }
            mustQuery.Add(q=>q.Bool(b=>b.Should(shouldQuery)));
        }
        
        
        QueryContainer Filter(QueryContainerDescriptor<Block> f) => f.Nested(q => q.Path("Transactions")
            .Query(qq => qq.Bool(b => b.Must(mustQuery))));
        // QueryContainer Filter(QueryContainerDescriptor<Block> f) => f.Nested(q => q.Path("Transactions")
        //     .Query(qq => qq.Bool(b => b.Must(s =>
        //         s.Match(i=>i.Field("Transactions.LogEvents.eventName").Query("IrreversibleBlockFound"))))));
        List<LogEventDto> resultList = new List<LogEventDto>();

        try
        {
            var list = await _blockIndexRepository.GetListAsync(Filter);

            var items = ObjectMapper.Map<List<Block>, List<BlockDto>>(list.Item2);

            var contractAddressList = input.Contracts.Select(i => i.ContractAddress);
            foreach (var blockItem in items)
            {
                foreach (var transactionItem in blockItem.Transactions)
                {
                    foreach (var logEventItem in transactionItem.LogEvents)
                    {
                        bool isWantedLogEvent = false;
                        foreach (var contractInputItem in input.Contracts)
                        {
                            if (!string.IsNullOrEmpty(contractInputItem.ContractAddress) && 
                                contractInputItem.EventNames.Count>0)
                            {
                                if (contractInputItem.ContractAddress==logEventItem.ContractAddress
                                    && contractInputItem.EventNames.Contains(logEventItem.EventName))
                                {
                                    isWantedLogEvent = true;
                                }
                            }
                            else if (!string.IsNullOrEmpty(contractInputItem.ContractAddress) &&
                                     contractInputItem.EventNames.Count <= 0)
                            {
                                if (contractInputItem.ContractAddress==logEventItem.ContractAddress)
                                {
                                    isWantedLogEvent = true;
                                }
                            }
                            else
                            {
                                if (contractInputItem.EventNames.Contains(logEventItem.EventName))
                                {
                                    isWantedLogEvent = true;
                                }
                            }
                        }
                        
                        if (isWantedLogEvent)
                        {
                            resultList.Add(logEventItem);
                        }
                    }

                    
                }
            }
            

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw e;
        }

        return resultList;
    }

}