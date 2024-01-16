using AeFinder.Etos;
using AutoMapper;

namespace AeFinder.EntityEventHandler.Core.Tests;

public class AeFinderAutoMapperProfile:Profile
{
    public AeFinderAutoMapperProfile()
    {
        CreateMap<NewBlockEto, ConfirmBlockEto>();
    }
    
}