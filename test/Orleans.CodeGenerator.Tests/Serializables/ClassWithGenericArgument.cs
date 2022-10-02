namespace Orleans.CodeGenerator.Tests.Serializables;

public class ClassWithGenericArgument : SerialiableGeneratorTestBase<ClassWithGenericArgument>
{
    public ClassWithGenericArgument(GeneratorTestFixture<ClassWithGenericArgument> fixture) : base(fixture)
    {
    }

    protected override string SourceText => @"
        [Orleans.GenerateSerializer]
        public class Test<T1> {
            [Orleans.Id(0)]
            public T1 A;
        }";

    protected override int TypeArguments => 1;
}
