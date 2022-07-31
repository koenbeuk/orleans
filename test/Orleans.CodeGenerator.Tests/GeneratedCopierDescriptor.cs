using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Orleans.CodeGenerator.Tests;
public class GeneratedCopierDescriptor
{
    public GeneratedCopierDescriptor(ClassDeclarationSyntax classDeclarationSyntax)
    {
        Classdeclarationsyntax = classDeclarationSyntax;
    }

    public ClassDeclarationSyntax Classdeclarationsyntax { get; }
}
