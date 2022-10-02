using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Orleans.CodeGenerator.Tests.Serializables;

public class ClassWithProperty : SerialiableGeneratorTestBase<ClassWithField>
{
    public ClassWithProperty(GeneratorTestFixture<ClassWithField> fixture) : base(fixture)
    {
    }

    protected override string SourceText => @"
        [Orleans.GenerateSerializer]
        public class Test {
            [Orleans.Id(0)]
            public int A { get; set; }
        }";
}