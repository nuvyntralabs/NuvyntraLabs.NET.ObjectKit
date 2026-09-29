using NuvyntraLabs.NET.ObjectKit;

namespace NuvyntraLabs.NET.ObjectKit.Tests;

public class ObjectKitTests
{
    [Fact]
    public void Copy_copies_values_without_sharing_the_instance()
    {
        var original = new Person { Name = "ada", Age = 36 };

        Person copy = original.Copy();
        copy.Age = 1;

        Assert.NotSame(original, copy);
        Assert.Equal("ada", copy.Name);
        Assert.Equal(36, original.Age);
        Assert.Equal(1, copy.Age);
    }

    [Fact]
    public void Equals_and_hash_code_follow_the_properties()
    {
        var left = new Person { Name = "ada", Age = 36 };
        var right = new Person { Name = "ada", Age = 36 };
        var other = new Person { Name = "ada", Age = 1 };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, other);
        Assert.True(left.Equals(left));
        Assert.False(left.Equals(null));
        Assert.False(left.Equals("ada"));
    }

    [Fact]
    public void Copy_covers_inherited_nullable_enum_keyword_and_empty_types()
    {
        var employee = new Employee { Name = "ada", Age = 4, Score = 9, Level = Level.Admin };
        Employee copy = employee.Copy();

        Assert.Equal("ada", copy.Name);
        Assert.Equal(4, copy.Age);
        Assert.Equal(9, copy.Score);
        Assert.Equal(Level.Admin, copy.Level);
        Assert.NotSame(employee, copy);

        var box = new KeywordBox { @class = 4 };
        Assert.Equal(4, box.Copy().@class);

        var empty = new EmptyBox();
        Assert.Equal(empty, empty.Copy());
        Assert.NotSame(empty, empty.Copy());
    }
}

public class Named
{
    public string Name { get; set; } = "";
}

public enum Level
{
    Guest,
    Admin,
}

[Copyable]
public partial class Employee : Named
{
    public int Age { get; set; }

    public int? Score { get; set; }

    public Level Level { get; set; }

    public static int Count { get; set; }

    public int this[int index]
    {
        get => index;
        set => _ = value;
    }
}

[Copyable]
public partial class EmptyBox
{
}

[Copyable]
public partial class KeywordBox
{
    public int @class { get; set; }
}

[Copyable]
public partial class Person
{
    public string Name { get; set; } = "";

    public int Age { get; set; }

    public DateOnly Born { get; init; }
}
