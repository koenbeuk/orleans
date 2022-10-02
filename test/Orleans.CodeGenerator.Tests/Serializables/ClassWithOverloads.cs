using Xunit;

namespace Orleans.CodeGenerator.Tests.Serializables;

public class ClassWithOverloads : GeneratorTestBase<ClassWithOverloads>
{
    public ClassWithOverloads(GeneratorTestFixture<ClassWithOverloads> fixture) : base(fixture)
    {
    }

    protected override string SourceText => @"
        [Orleans.GenerateSerializer]
        public class Test { }

        [Orleans.GenerateSerializer]
        public class Test<T1> { }";

    [Fact]
    public void HasGeneratedMetadataForNonGenericOverload()
    {
        AssertGeneratedArtifact("Metadata_Test_0", "Test", 0);
    }

    [Fact]
    public void HasGeneratedMetadataNonGenericOverload()
    {
        AssertGeneratedArtifact("Metadata_Test_1", "Test", 1);
    }
}