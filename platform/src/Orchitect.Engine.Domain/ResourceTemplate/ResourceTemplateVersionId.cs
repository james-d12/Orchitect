namespace Orchitect.Engine.Domain.ResourceTemplate;

public readonly record struct ResourceTemplateVersionId(Guid Value)
{
    public ResourceTemplateVersionId() : this(Guid.NewGuid())
    {
    }
}