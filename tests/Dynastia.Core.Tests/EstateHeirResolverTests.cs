using Dynastia.Contracts;
using Dynastia.Mechanics.Inheritance;

namespace Dynastia.Core.Tests;

public sealed class EstateHeirResolverTests
{
    [Fact]
    public void ExistingPriorityRemainsChildrenSpouseSiblingsParentsCousinsThenAuntsAndUncles()
    {
        using var f = new RefactorFixture();
        var source = f.Person(60, alive: false);
        var child = f.Person(30);
        var spouse = f.Person(58);
        var sibling = f.Person(55);
        var father = f.Person(85);
        var mother = f.Person(83);
        var grandparent = f.Person(110, alive: false);
        var aunt = f.Person(80);
        var cousin = f.Person(45);
        f.Family.SetParents(child, source, null);
        f.Family.SetSpouses(source, spouse, 1860);
        f.Family.SetParents(source, father, mother);
        f.Family.SetParents(sibling, father, null); // Half-sibling remains eligible.
        f.Family.SetParents(father, grandparent, null);
        f.Family.SetParents(aunt, grandparent, null);
        f.Family.SetParents(cousin, aunt, null);
        var resolver = new EstateHeirResolver(f.Family);
        AssertResolution("the living children", child);
        Die(child);
        AssertResolution("the surviving spouse", spouse);
        Die(spouse);
        AssertResolution("the living siblings", sibling);
        Die(sibling);
        AssertResolution("the living parents", father, mother);
        Die(father); Die(mother);
        AssertResolution("the living first cousins", cousin);
        Die(cousin);
        AssertResolution("the living uncles and aunts", aunt);
        Die(aunt);
        AssertResolution("no heirs");
        Assert.Empty(resolver.Resolve(f.State, null).Heirs);

        void AssertResolution(string description, params IPerson[] expected)
        {
            var result = resolver.Resolve(f.State, source);
            Assert.Equal(description, result.Description);
            Assert.Equal(expected.Select(person => person.Id), result.Heirs.Select(person => person.Id));
        }
    }

    [Fact]
    public void OrderUsesFullBirthDateThenIdentityRegardlessOfLinkOrder()
    {
        using var f = new RefactorFixture();
        var source = f.Person(70, alive: false);
        var laterDay = f.Person(30);
        var earlierId = f.Person(30);
        var laterId = f.Person(30);
        var unknownBirth = f.Person(90);
        laterDay.BirthDate = new GameDate(1850, 2, 2);
        earlierId.BirthDate = laterId.BirthDate = new GameDate(1850, 2, 1);
        unknownBirth.BirthDate = null;
        foreach (var heir in new[] { unknownBirth, laterId, laterDay, earlierId })
            f.Family.SetParents(heir, source, null);
        Assert.Equal(new[] { earlierId.Id, laterId.Id, laterDay.Id, unknownBirth.Id },
            new EstateHeirResolver(f.Family).Resolve(f.State, source).Heirs.Select(heir => heir.Id));
    }

    private static void Die(IPerson person)
    {
        person.Tags.Remove("state.alive");
        person.Tags.Add("state.dead");
    }
}
