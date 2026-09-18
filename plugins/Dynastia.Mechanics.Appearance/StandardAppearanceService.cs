using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Appearance;

public sealed class StandardAppearanceService : IAppearanceService
{
    private readonly IFamilyService _family;

    public StandardAppearanceService(IFamilyService family)
    {
        _family = family;
    }

    public AppearanceSnapshot EnsureAppearance(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var existing = person.Components.Get<AppearanceComponent>();
        if (existing is not null)
            return ToSnapshot(existing);

        var sex = _family.GetSex(person);
        var father = _family.GetFather(person);
        var mother = _family.GetMother(person);

        AppearanceSnapshot generated;

        if (father is null && mother is null)
        {
            generated = GenerateCandidateAppearance(person.Id, sex);
        }
        else
        {
            generated = GenerateInheritedAppearance(
                person.Id,
                sex,
                father,
                mother);
        }

        SetAppearance(person, generated);
        return generated;
    }

    public AppearanceSnapshot GenerateCandidateAppearance(
        Guid deterministicId,
        Sex sex)
    {
        var geneA = RollHairGene(deterministicId, "hair-gene-a");
        var geneB = RollHairGene(deterministicId, "hair-gene-b");
        var visible = ResolveHairColor(
            geneA,
            geneB,
            deterministicId);

        var texture = Roll01(deterministicId, "texture") < 0.16
            ? HairTexture.Curly
            : HairTexture.Straight;

        var greying = RollInt(
            deterministicId,
            "greying",
            40,
            66);

        var baldingRoll = Roll01(deterministicId, "balding");
        var balding = baldingRoll switch
        {
            < 0.46 => BaldingTendency.None,
            < 0.84 => BaldingTendency.Mild,
            _ => BaldingTendency.Strong
        };

        var beard = sex == Sex.Male
            ? ResolveBaseBeard(deterministicId)
            : BeardDensity.None;

        return new AppearanceSnapshot(
            geneA,
            geneB,
            visible,
            texture,
            greying,
            balding,
            beard);
    }

    public void SetAppearance(
        IPerson person,
        AppearanceSnapshot appearance,
        Guid? portraitSeedId = null)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(appearance);

        person.Components.Set(
            new AppearanceComponent
            {
                HairGeneA = appearance.HairGeneA,
                HairGeneB = appearance.HairGeneB,
                HairColor = appearance.HairColor,
                HairTexture = appearance.HairTexture,
                GreyingStartAge = Math.Clamp(
                    appearance.GreyingStartAge,
                    35,
                    70),
                BaldingTendency = appearance.BaldingTendency,
                BeardDensity = appearance.BeardDensity,
                PortraitSeedId = portraitSeedId
            });
    }

    public string GetPortrait(
        IPerson person,
        bool useDeadOverride = true)
    {
        ArgumentNullException.ThrowIfNull(person);

        var component = person.Components.Get<AppearanceComponent>()
            ?? throw new InvalidOperationException(
                $"Appearance state is missing for {_family.GetDisplayName(person)}. " +
                "Run state reconciliation before reading portraits.");

        return GetPortrait(
            ToSnapshot(component),
            _family.GetSex(person),
            person.Age,
            component.PortraitSeedId ?? person.Id,
            useDeadOverride && person.Tags.Has("state.dead"));
    }

    public string GetPortrait(
        AppearanceSnapshot appearance,
        Sex sex,
        int age,
        Guid deterministicId,
        bool isDead = false)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        // Portraits intentionally use a skin-tone-qualified emoji set.
        // This avoids the platform-default yellow presentation and lets the
        // available Unicode hair variants read as actual physical portraits.
        // Unicode has no separate straight black-hair and brown-hair variants,
        // so both use the standard dark-hair face.
        if (age <= 4)
            return "👶🏻";

        if (age >= 70)
        {
            return sex == Sex.Male
                ? "👴🏻"
                : "👵🏻";
        }

        var beard = sex == Sex.Male
            && ShouldShowBeard(
                appearance.BeardDensity,
                age,
                deterministicId);

        if (age <= 11)
        {
            if (appearance.HairColor == HairColor.Red)
                return "🧑🏻‍🦰";

            if (appearance.HairColor == HairColor.Blond)
            {
                return sex == Sex.Male
                    ? "👱🏻‍♂️"
                    : "👱🏻‍♀️";
            }

            if (appearance.HairTexture == HairTexture.Curly)
                return "🧑🏻‍🦱";

            return sex == Sex.Male
                ? "👦🏻"
                : "👧🏻";
        }

        if (age <= 17)
        {
            // Preserve visible hair colour where Unicode can express it.
            // A generic bearded emoji would otherwise erase red/blond hair.
            if (appearance.HairColor == HairColor.Red)
                return "🧑🏻‍🦰";

            if (appearance.HairColor == HairColor.Blond)
            {
                return sex == Sex.Male
                    ? "👱🏻‍♂️"
                    : "👱🏻‍♀️";
            }

            if (appearance.HairTexture == HairTexture.Curly)
                return "🧑🏻‍🦱";

            if (beard)
                return "🧔🏻‍♂️";

            return "🧑🏻";
        }

        var isGrey = ShouldShowGreyHair(
            appearance.GreyingStartAge,
            age,
            deterministicId);

        var isBald = sex == Sex.Male
            && ShouldShowBalding(
                appearance.BaldingTendency,
                age,
                deterministicId);

        if (isBald)
            return "👨🏻‍🦲";

        if (isGrey)
        {
            return sex == Sex.Male
                ? "👨🏻‍🦳"
                : "👩🏻‍🦳";
        }

        if (appearance.HairColor == HairColor.Red)
        {
            return sex == Sex.Male
                ? "👨🏻‍🦰"
                : "👩🏻‍🦰";
        }

        if (appearance.HairColor == HairColor.Blond)
        {
            return sex == Sex.Male
                ? "👱🏻‍♂️"
                : "👱🏻‍♀️";
        }

        if (appearance.HairTexture == HairTexture.Curly)
        {
            return sex == Sex.Male
                ? "👨🏻‍🦱"
                : "👩🏻‍🦱";
        }

        if (beard)
            return "🧔🏻‍♂️";

        return sex == Sex.Male
            ? "👨🏻"
            : "👩🏻";
    }

    private AppearanceSnapshot GenerateInheritedAppearance(
        Guid childId,
        Sex sex,
        IPerson? father,
        IPerson? mother)
    {
        var fatherAppearance = father is null
            ? null
            : EnsureAppearance(father);
        var motherAppearance = mother is null
            ? null
            : EnsureAppearance(mother);

        var geneA = fatherAppearance is null
            ? RollHairGene(childId, "missing-father-gene")
            : SelectParentGene(
                fatherAppearance,
                childId,
                "father-gene");

        var geneB = motherAppearance is null
            ? RollHairGene(childId, "missing-mother-gene")
            : SelectParentGene(
                motherAppearance,
                childId,
                "mother-gene");

        var visible = ResolveHairColor(
            geneA,
            geneB,
            childId);

        var curlyParents =
            (fatherAppearance?.HairTexture == HairTexture.Curly ? 1 : 0)
            + (motherAppearance?.HairTexture == HairTexture.Curly ? 1 : 0);

        var curlyChance = curlyParents switch
        {
            2 => 0.64,
            1 => 0.38,
            _ => 0.09
        };

        var texture = Roll01(childId, "inherited-texture") < curlyChance
            ? HairTexture.Curly
            : HairTexture.Straight;

        var parentGreying = new List<int>();
        if (fatherAppearance is not null)
            parentGreying.Add(fatherAppearance.GreyingStartAge);
        if (motherAppearance is not null)
            parentGreying.Add(motherAppearance.GreyingStartAge);

        var greyingBase = parentGreying.Count == 0
            ? RollInt(childId, "greying-base", 40, 66)
            : (int)Math.Round(parentGreying.Average());
        var greying = Math.Clamp(
            greyingBase + RollInt(childId, "greying-variation", -5, 5),
            35,
            70);

        var fatherBalding = fatherAppearance is null
            ? (int)RollBaldingTendency(childId, "missing-father-balding")
            : (int)fatherAppearance.BaldingTendency;
        var motherBalding = motherAppearance is null
            ? (int)RollBaldingTendency(childId, "missing-mother-balding")
            : (int)motherAppearance.BaldingTendency;

        var baldingScore =
            ((fatherBalding * 2.0) + motherBalding) / 3.0
            + RollSignedVariation(childId, "balding-variation") * 0.45;
        var balding = (BaldingTendency)Math.Clamp(
            (int)Math.Round(baldingScore),
            0,
            2);

        var beard = BeardDensity.None;
        if (sex == Sex.Male)
        {
            var fatherBeard = fatherAppearance is null
                ? (int)ResolveBaseBeard(childId)
                : (int)fatherAppearance.BeardDensity;

            beard = (BeardDensity)Math.Clamp(
                fatherBeard + RollSignedVariation(
                    childId,
                    "beard-variation"),
                0,
                3);
        }

        return new AppearanceSnapshot(
            geneA,
            geneB,
            visible,
            texture,
            greying,
            balding,
            beard);
    }

    private static HairColor SelectParentGene(
        AppearanceSnapshot appearance,
        Guid childId,
        string salt)
    {
        return Roll01(childId, salt) < 0.5
            ? appearance.HairGeneA
            : appearance.HairGeneB;
    }

    private static HairColor ResolveHairColor(
        HairColor first,
        HairColor second,
        Guid deterministicId)
    {
        if (first == HairColor.Red && second == HairColor.Red)
            return HairColor.Red;

        var redBlond =
            (first == HairColor.Red && second == HairColor.Blond)
            || (first == HairColor.Blond && second == HairColor.Red);

        if (redBlond)
        {
            return Roll01(deterministicId, "red-blond-expression") < 0.45
                ? HairColor.Red
                : HairColor.Blond;
        }

        if (first == HairColor.Black || second == HairColor.Black)
            return HairColor.Black;

        if (first == HairColor.Brown || second == HairColor.Brown)
            return HairColor.Brown;

        if (first == HairColor.Blond || second == HairColor.Blond)
            return HairColor.Blond;

        return HairColor.Red;
    }

    private static HairColor RollHairGene(Guid id, string salt)
    {
        var roll = Roll01(id, salt);

        // Baseline gene frequencies are deliberately lighter than the
        // visible-hair distribution because black and brown are dominant.
        // With the expression rules below this yields roughly 19% black,
        // 45% brown, 32% blond and 4% red visible hair in unrelated adults.
        return roll switch
        {
            < 0.10 => HairColor.Black,
            < 0.40 => HairColor.Brown,
            < 0.92 => HairColor.Blond,
            _ => HairColor.Red
        };
    }

    private static BaldingTendency RollBaldingTendency(
        Guid id,
        string salt)
    {
        var roll = Roll01(id, salt);

        return roll switch
        {
            < 0.46 => BaldingTendency.None,
            < 0.84 => BaldingTendency.Mild,
            _ => BaldingTendency.Strong
        };
    }

    private static BeardDensity ResolveBaseBeard(Guid id)
    {
        var roll = Roll01(id, "beard-density");

        return roll switch
        {
            < 0.16 => BeardDensity.None,
            < 0.38 => BeardDensity.Light,
            < 0.78 => BeardDensity.Normal,
            _ => BeardDensity.Strong
        };
    }

    private static bool ShouldShowBeard(
        BeardDensity density,
        int age,
        Guid id)
    {
        if (age < 16 || density == BeardDensity.None)
            return false;

        var chance = density switch
        {
            BeardDensity.Light => 0.25,
            BeardDensity.Normal => 0.62,
            BeardDensity.Strong => 0.92,
            _ => 0.0
        };

        return Roll01(id, "visible-beard") < chance;
    }

    private static bool ShouldShowBalding(
        BaldingTendency tendency,
        int age,
        Guid id)
    {
        if (tendency == BaldingTendency.None || age < 18)
            return false;

        var onset = tendency == BaldingTendency.Strong
            ? 30 + RollInt(id, "strong-balding-onset", 0, 12)
            : 45 + RollInt(id, "mild-balding-onset", 0, 15);

        return age >= onset;
    }

    private static bool ShouldShowGreyHair(
        int greyingStartAge,
        int age,
        Guid id)
    {
        var start = Math.Clamp(greyingStartAge, 35, 70);
        if (age < start)
            return false;

        var range = Math.Max(1, 70 - start);
        var progress = Math.Clamp(
            (age - start) / (double)range,
            0,
            1);

        return Roll01(id, "grey-hair-threshold") <= progress;
    }

    private static AppearanceSnapshot ToSnapshot(
        AppearanceComponent component)
    {
        return new AppearanceSnapshot(
            component.HairGeneA,
            component.HairGeneB,
            component.HairColor,
            component.HairTexture,
            component.GreyingStartAge,
            component.BaldingTendency,
            component.BeardDensity);
    }

    private static int RollSignedVariation(Guid id, string salt)
    {
        var roll = Roll01(id, salt);
        return roll < 0.22 ? -1 : roll > 0.78 ? 1 : 0;
    }

    private static int RollInt(
        Guid id,
        string salt,
        int minimum,
        int maximum)
    {
        if (minimum > maximum)
            (minimum, maximum) = (maximum, minimum);

        var span = (long)maximum - minimum + 1;
        var value = DeterministicUInt64(id, salt);
        return minimum + (int)(value % (ulong)span);
    }

    private static double Roll01(Guid id, string salt)
    {
        var value = DeterministicUInt64(id, salt);
        return value / (double)ulong.MaxValue;
    }

    private static ulong DeterministicUInt64(Guid id, string salt)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{id:N}|{salt}"));

        return BitConverter.ToUInt64(bytes, 0);
    }
}
