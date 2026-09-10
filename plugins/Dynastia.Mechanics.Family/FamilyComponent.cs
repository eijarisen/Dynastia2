using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class FamilyComponent
{
    public Sex Sex { get; set; }

    public int? Generation { get; set; }

    public Guid? FatherId { get; set; }
    public Guid? MotherId { get; set; }
    public Guid? SpouseId { get; set; }

    public List<Guid> ChildrenIds { get; } = [];
}
