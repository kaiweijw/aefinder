using AElfScan.AElf.Dtos;
using AElfScan.AElf.Entities.Es;

namespace AElfScan.Orleans.EventSourcing.Grain.BlockScan;

public interface IBlockFilterProvider
{
    BlockFilterType FilterType { get; }

    Task<List<BlockDto>> GetBlocksAsync(string chainId, long startBlockNumber, long endBlockNumber, bool onlyConfirmed,
        List<FilterContractEventInput> filters);
    
    Task<List<BlockDto>> FilterBlocksAsync(List<BlockDto> blocks, List<FilterContractEventInput> filters);
}