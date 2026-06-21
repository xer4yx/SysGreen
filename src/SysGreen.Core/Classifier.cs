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
        if (entry.ExecutablePath is { } path)
        {
            var exeName = Path.GetFileName(path);
            var hit = _knowledgeBase.Match(exeName, entry.Publisher, path);
            if (hit is not null)
            {
                return new Classification(
                    hit.Purpose, hit.Safety, ClassificationSource.KnowledgeBase,
                    hit.Description, hit.ProvidesPassiveValue);
            }
        }

        // TODO: heuristic fallback from file metadata (signature/publisher, description,
        // install path, updater patterns). Until then, unknowns are Caution and never
        // auto-recommended (ADR-0002).
        return Classification.UnknownCaution;
    }
}
