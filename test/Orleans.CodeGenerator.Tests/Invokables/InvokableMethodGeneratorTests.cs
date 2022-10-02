using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Orleans.CodeGenerator.Tests.Invokables;
public class InvokableMethodGeneratorTests : GeneratorTestBase<InvokableMethodGeneratorTests>
{
    public InvokableMethodGeneratorTests(GeneratorTestFixture<InvokableMethodGeneratorTests> fixture) : base(fixture)
    {
    }

    protected override string SourceText => @"
        public interface ITest : Orleans.IGrain {
            System.Threading.Tasks.Task SimpleMethod();
        }
        ";

    [Fact]
    public void HasGeneratedMethodInvoker()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedMethodInvoker = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Invokable_ITest_GrainReference_1_0");

        Assert.NotNull(generatedMethodInvoker);
    }

    [Fact]
    public void HasGeneratedProxy()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedMethodInvoker = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Proxy_ITest");

        Assert.NotNull(generatedMethodInvoker);
    }

    [Fact]
    public void HasGeneratedMethodInvokerCodec()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedMethodInvoker = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Codec_Invokable_ITest_GrainReference_1_0");

        Assert.NotNull(generatedMethodInvoker);
    }

    [Fact]
    public void HasGeneratedMethodInvokerCopier()
    {
        AssertGeneratedCopier("Copier_Invokable_ITest_GrainReference_1_0", "ITest", 0);
    }

    [Fact]
    public void HasGeneratedMethodInvokerActivator()
    {
        AssertGeneratedArtifact("Activator_Invokable_ITest_GrainReference_1_0", "ITest", 0);
    }


    [Fact]
    public void HasGeneratedMetadata()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedSerializer = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Metadata_ITest_0");

        Assert.NotNull(generatedSerializer);
    }

}
