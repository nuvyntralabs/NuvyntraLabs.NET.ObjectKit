# NuvyntraLabs.NET.ObjectKit

Generate a structural copy and equality for a marked class without reflection.

**Version:** 0.1.1. Not published to nuget.org yet. Do not `dotnet nuget push` from a local clone.

Docs: https://nuvyntralabs.github.io/packages/nuvyntralabs-net-objectkit/

```bash
dotnet add package NuvyntraLabs.NET.ObjectKit
```

```csharp
[Copyable]
public partial class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public int? Score { get; set; }
    public Level Level { get; set; }
    public DateOnly Born { get; init; }
}

Person copy = person.Copy();
bool same = person.Equals(copy);
```

The type must be a non-generic, non-nested, concrete `partial` class with a public parameterless constructor. Copied members are public properties with a public getter and a public `set` or `init` accessor. Supported types are `string`, enums, value types, and `Nullable<T>`. Inherited properties are copied. Static properties and indexers are skipped. A keyword property name is emitted with `@`.

If the generator cannot copy the type, the build fails. There is no reflection fallback.

| Id | The build fails when |
| --- | --- |
| OBK001 | The type is not a class. |
| OBK002 | The class is not `partial`. |
| OBK003 | The class is abstract or static. |
| OBK004 | The class is generic. |
| OBK005 | The class is nested. |
| OBK006 | There is no public parameterless constructor. |
| OBK007 | A property has no public getter. |
| OBK008 | A property has no public `set` or `init` accessor. |
| OBK009 | A property type is not a string, enum, or value type. |

The console sample copies a class with inherited, nullable, enum, and `init` properties:

```bash
dotnet run --project samples/ObjectKit.Sample
dotnet test NuvyntraLabs.NET.ObjectKit.sln
```

Prefer first: [Mapperly](https://github.com/riok/mapperly) for object-to-object mapping.

Target frameworks: `net8.0`, `net9.0`, and `net10.0`. The generator ships inside the package. Nullable, trim, and Native AOT compatible.

Author: Niladri Prasad Padhy. License: MIT.
