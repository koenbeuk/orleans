using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Orleans.CodeGenerator.Tests.Serializables;

public class ClassWithField : SerialiableGeneratorTestBase<ClassWithField>
{
    public ClassWithField(GeneratorTestFixture<ClassWithField> fixture) : base(fixture)
    {
    }

    protected override string SourceText => @"
        [Orleans.GenerateSerializer]
        public class Test {
            [Orleans.Id(0)]
            public int A;
        }";
}