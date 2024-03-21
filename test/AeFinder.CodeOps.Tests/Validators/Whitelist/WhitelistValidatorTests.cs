using GraphQL;
using Microsoft.Extensions.Logging;
using Nest;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.ObjectMapping;
using Xunit;

namespace AeFinder.CodeOps.Validators.Whitelist;

public class WhitelistValidatorTests : AeFinderCodeOpsTestBase
{
    private readonly WhitelistValidator _whitelistValidator;

    public WhitelistValidatorTests()
    {
        _whitelistValidator = GetRequiredService<WhitelistValidator>();
    }

    [Fact]
    public void ValidateTest()
    {
        var sourceCode = @"
        using AeFinder.Sdk;
        using AeFinder.Sdk.Processor;
        using GraphQL;
        using Volo.Abp.Modularity;
        using Volo.Abp.ObjectMapping;
        using System;
        using System.Threading.Tasks;
        using System.Linq;
        using System.Collections.Generic;
        using Nest;

        namespace TestApp;

        public class TestAppEntity : AeFinderEntity, IAeFinderEntity
        {
            public int IntValue { get; set; }
            [Keyword]
            public string StringValue { get; set; }
            [Text]
            public string StringValue2 { get; set; }
        }
        
        public class TestAppEntityDto
        {
            
        }

        public class Query
        {
            public static async Task<List<TestAppEntityDto>> TokenInfo(
                [FromServices] IReadOnlyRepository<TestAppEntity> repository,
                [FromServices] IObjectMapper objectMapper, string chainId)
            {
                var query = await repository.GetQueryableAsync();
                var list = query.ToList();

                return objectMapper.Map<List<TestAppEntity>, List<TestAppEntityDto>>(list);
            }
        }

        public class TestAppSchema : AppSchema<Query>
        {
            protected TestAppSchema(IServiceProvider serviceProvider) : base(serviceProvider)
            {
            }
        }

        public class TestAppBlockProcessor : BlockProcessorBase
        {
            public override async Task ProcessAsync(AeFinder.Sdk.Processor.Block block)
            {
                throw new NotImplementedException();
            }
        }

        public class TestAppModule:AbpModule
        {
            public override void ConfigureServices(ServiceConfigurationContext context)
            {
            }
        }
        ";
        AddAssemblies(typeof(FromServicesAttribute).Assembly.Location, typeof(IObjectMapper).Assembly.Location,
            typeof(AbpModule).Assembly.Location, typeof(KeywordAttribute).Assembly.Location);
        var assemblyDefinition = CompileToAssemblyDefinition(sourceCode);

        var validationResult = _whitelistValidator.Validate(assemblyDefinition.MainModule, CancellationToken.None);
        validationResult.Count().ShouldBe(0);
    }

    [Fact]
    public void Validate_Failed_Test()
    {
        var sourceCode = @"
        using AeFinder.Sdk;
        using AeFinder.Sdk.Processor;
        using Nest;
        using System.Net.Http;
        using Microsoft.Extensions.Logging;
        using System.Threading.Tasks;
        using System;

        namespace TestApp;

        public class TestAppEntity : AeFinderEntity, IAeFinderEntity
        {
            public int IntValue { get; set; }
            [Keyword]
            public string StringValue { get; set; }
        }

        public class Client
        {
            public HttpClient GetClient()
            {
                return new HttpClient();
            }
        }

        public class TestAppBlockProcessor : BlockProcessorBase
        {
            private readonly ILogger<TestAppBlockProcessor> _logger;

            public override async Task ProcessAsync(AeFinder.Sdk.Processor.Block block)
            {
                throw new NotImplementedException();
            }
        }
        ";
        AddAssemblies(typeof(FromServicesAttribute).Assembly.Location, typeof(IObjectMapper).Assembly.Location,
            typeof(AbpModule).Assembly.Location, typeof(KeywordAttribute).Assembly.Location, typeof(ILogger).Assembly.Location);
        var assemblyDefinition = CompileToAssemblyDefinition(sourceCode);

        var validationResults = _whitelistValidator.Validate(assemblyDefinition.MainModule, CancellationToken.None)
            .ToList();
        validationResults.Count.ShouldBeGreaterThan(0);
        validationResults.ShouldContain(v => v.Message == "Assembly System.Net.Http is not allowed.");
        validationResults.ShouldContain(v => v.Message == "Assembly Microsoft.Extensions.Logging.Abstractions is not allowed.");
        validationResults.ShouldContain(v => v.Message == "System.Net.Http is not allowed.");
    }
}