namespace Orchitect.Domain.Engine.ResourceTemplate;

public readonly record struct ResourceTemplateVersionId(Guid Value)
{
    public ResourceTemplateVersionId() : this(Guid.NewGuid())
    {
    }
}