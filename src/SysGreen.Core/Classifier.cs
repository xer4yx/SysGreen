using SysGreen.Core.Domain;

namespace SysGreen.Core.Knowledge;

/// <summary>
/// Default classifier. Precedence: Knowledge Base → heuristic fallback → Unknown/Caution.
/// (User Override precedence is layered above this once the override store is wired.)
/// See ADR-0002.
/// </summary>
public sealed class Classifier : IClassifier
{
    private readonly IKnowledgeBase _knowledgeBase;

    public Classifier(IKnowledgeBase knowledgeBase) => _knowledgeBase = knowledgeBase;

    public Classification Classify(AutostartEntry entry)
    {
        foreach (var exeName in CandidateNames(entry))
        {
            var hit = _knowledgeBase.Match(exeName, entry.Publisher, entry.ExecutablePath);
            if (hit is not null)
            {
                return new Classification(
                    hit.Purpose, hit.Safety, ClassificationSource.KnowledgeBase,
                    hit.Description, hit.ProvidesPassiveValue);
            }
        }

        // Knowledge Base missed — infer from file metadata (signer, install path, updater patterns).
        // Anything the heuristic can't place confidently stays Unknown/Caution (ADR-0002).
        return HeuristicClassifier.Classify(entry) ?? Classification.UnknownCaution;
    }

    /// <summary>
    /// KB match candidates in priority order: the --processStart target (the real app, for
    /// Squirrel-style launchers) before the launcher executable's own filename (ADR-0010).
    /// </summary>
    private static IEnumerable<string> CandidateNames(AutostartEntry entry)
    {
        if (entry.TargetExecutable is { Length: > 0 } target) yield return target;
        if (entry.ExecutablePath is { } path) yield return Path.GetFileName(path);
    }
}
