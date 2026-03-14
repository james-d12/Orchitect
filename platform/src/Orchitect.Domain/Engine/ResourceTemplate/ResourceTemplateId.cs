namespace Orchitect.Domain.Engine.ResourceTemplate;

public readonly record struct ResourceTemplateId(Guid Value)
{
    public ResourceTemplateId() : this(Guid.NewGuid())
    {
    }
}