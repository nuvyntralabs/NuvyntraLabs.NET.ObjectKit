using NuvyntraLabs.NET.ObjectKit;

var original = new Person { Name = "ada", Age = 36, Level = Level.Admin, Score = 10, Born = new DateOnly(1990, 1, 2) };
Person copy = original.Copy();
copy.Age = 1;
var empty = new EmptyBox();
var keyword = new KeywordBox { @class = 4 };

Console.WriteLine($"{copy.Name} {copy.Level} {copy.Score} {copy.Born:yyyy-MM-dd} {original.Equals(copy)} {original.Equals(original)}");
Console.WriteLine($"{empty.Equals(empty.Copy())} {keyword.Copy().@class}");

public enum Level
{
    Guest,
    Admin,
}

public class Named
{
    public string Name { get; set; } = "";
}

/// <summary>Sample type copied by ObjectKit.</summary>
[Copyable]
public partial class Person : Named
{
    /// <summary>Age in years.</summary>
    public int Age { get; set; }

    /// <summary>Optional score.</summary>
    public int? Score { get; set; }

    /// <summary>Access level.</summary>
    public Level Level { get; set; }

    /// <summary>Birth date.</summary>
    public DateOnly Born { get; init; }

    /// <summary>Ignored by the generator.</summary>
    public static int Count { get; set; }

    /// <summary>Ignored by the generator.</summary>
    public int this[int index]
    {
        get => index;
        set => _ = value;
    }
}

/// <summary>Type with no properties.</summary>
[Copyable]
public partial class EmptyBox
{
}

/// <summary>Type whose property name is a keyword.</summary>
[Copyable]
public partial class KeywordBox
{
    /// <summary>Keyword property.</summary>
    public int @class { get; set; }
}
