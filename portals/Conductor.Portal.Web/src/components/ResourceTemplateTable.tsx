import { useEffect, useState } from "react";
import { apiRequest } from "../utils/api";

export interface ResourceTemplateItem {
  id: string;
  name: string;
  type: string;
  version: string;
  updatedAt: string;
}

interface ResourceTemplatesResponse {
    resourceTemplates: ResourceTemplateItem[];
}

export default function ResourceTemplatesTable() {
  const [items, setItems] = useState<ResourceTemplateItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function load() {
      try {
        const data = await apiRequest<ResourceTemplatesResponse>(
          "/resource-templates",
          {
            method: "GET",
            requiresAuth: true,
          }
        );
        setItems(data.resourceTemplates);
      } catch (err) {
        console.error(err);
      } finally {
        setLoading(false);
      }
    }

    load();
  }, []);

  if (loading) return <div className="p-4">Loading...</div>;

  return (
    <div className="p-4">
      <h1 className="text-2xl font-semibold mb-4">Resource Templates</h1>

      <div className="overflow-x-auto rounded-xl border border-gray-200">
        <table className="w-full text-left text-sm">
          <thead className="bg-gray-100 text-gray-700">
            <tr>
              <th className="p-3">Name</th>
              <th className="p-3">Type</th>
              <th className="p-3">Version</th>
              <th className="p-3">Last Updated</th>
              <th className="p-3">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className="border-t hover:bg-gray-50">
                <td className="p-3 font-medium">{item.name}</td>
                <td className="p-3">{item.type}</td>
                <td className="p-3">{item.version}</td>
                <td className="p-3">{item.updatedAt}</td>
                <td className="p-3 space-x-2">
                  <button className="px-3 py-1 rounded bg-blue-600 text-white text-xs">
                    Edit
                  </button>
                  <button className="px-3 py-1 rounded bg-red-600 text-white text-xs">
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
