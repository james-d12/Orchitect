export interface ResourceTemplateItem {
  id: string;
  name: string;
  type: string;
  version: string;
  updatedAt: string;
}

const ENGINE_API_BASE_URL = import.meta.env.ENGINE_API_BASE_URL;

export async function fetchResourceTemplates(): Promise<
  ResourceTemplateItem[]
> {
  const res = await fetch(`${ENGINE_API_BASE_URL}/resource-templates`);
  if (!res.ok) throw new Error("Failed to fetch resource templates");
  return res.json();
}

export async function createResourceTemplate(
  item: Partial<ResourceTemplateItem>,
) {
  const res = await fetch(`${ENGINE_API_BASE_URL}/resource-templates`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(item),
  });
  if (!res.ok) throw new Error("Failed to create resource template");
  return res.json();
}

export async function updateResourceTemplate(
  id: string,
  item: Partial<ResourceTemplateItem>,
) {
  const res = await fetch(`${ENGINE_API_BASE_URL}/resource-templates/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(item),
  });
  if (!res.ok) throw new Error("Failed to update resource template");
  return res.json();
}

export async function deleteResourceTemplate(id: string) {
  const res = await fetch(`${ENGINE_API_BASE_URL}/resource-templates/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error("Failed to delete resource template");
  return res.json();
}
