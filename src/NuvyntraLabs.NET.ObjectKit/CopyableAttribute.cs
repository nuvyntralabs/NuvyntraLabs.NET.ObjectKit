namespace NuvyntraLabs.NET.ObjectKit;

/// <summary>
/// Marks a non-generic partial class for source-generated <c>Copy</c>, <c>Equals</c>, and <c>GetHashCode</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CopyableAttribute : Attribute
{
}
