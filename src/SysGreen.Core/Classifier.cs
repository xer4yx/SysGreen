using SysGreen.Core.Domain;

namespace SysGreen.Core.Knowledge;

/// <summary>
/// Default classifier. Precedence: Knowledge Base → heuristic fallback → Unknown/Caution. User
/// Override sits above this via the <see cref="OverridingClassifier"/> decorator. See ADR-0002.
/// </summary>
public sealed class Classifier : IClassifier
{
    private readonly IKnowledgeBase _knowledgeBase;

    public Classifier(IKnowledgeBase knowledgeBase) => _knowledgeBase = knowledgeBase;

    public Classification Classify(AutostartEntry entry)
    {
        // Candidates in priority order (target before launcher) so KB and Override agree on identity.
        foreach (var exeName in ExecutableIdentity.CandidateNames(entry))
        {
            var hit = _knowledgeBase.Match(exeName, entry.Publisher, entry.ExecutablePath);
            if (hit is not null)
            {
                return new Classification(
                    hit.Purpose, hit.Safety, ClassificationSource.KnowledgeBase,
                    hit.Description, hit.ProvidesPassiveValue)
                { TypicalRamBytes = hit.TypicalRamBytes };
            }
        }

        // Knowledge Base missed — infer from file metadata (signer, install path, updater patterns).
        // Anything the heuristic can't place confidently stays Unknown/Caution (ADR-0002).
        return HeuristicClassifier.Classify(entry) ?? Classification.UnknownCaution;
    }
}
