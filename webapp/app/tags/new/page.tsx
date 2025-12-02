import { createTag } from "@/lib/actions/tag-actions";
import { redirect } from "next/navigation";

async function create(formData: FormData) {
  "use server";
  const name = formData.get("name")?.toString().trim();
  const description = formData.get("description")?.toString().trim();
  const slug = formData.get("slug")?.toString().trim();

  if (!name || !description) return; // basic validation, could expand

  const res = await createTag({ name, description, slug: slug || undefined });
  if (res.error) {
    // TODO: surface error (could use cookies / search params). For now abort.
    return;
  }
  redirect("/tags");
}

export default function NewTagPage() {
  return (
    <div className="max-w-xl space-y-6 p-6">
      <h1 className="text-2xl font-semibold">Create Tag</h1>
      <form action={create} className="flex flex-col gap-4">
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Name *</span>
          <input
            name="name"
            required
            className="border rounded px-3 py-2"
            placeholder="e.g. javascript"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Slug (optional)</span>
          <input
            name="slug"
            className="border rounded px-3 py-2"
            placeholder="auto-generated if blank"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Description *</span>
          <textarea
            name="description"
            required
            rows={5}
            className="border rounded px-3 py-2"
            placeholder="Describe the tag's purpose"
          />
        </label>
        <div className="flex gap-3">
          <button
            type="submit"
            className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
          >
            Create
          </button>
          <a href="/tags" className="px-4 py-2 rounded border">
            Cancel
          </a>
        </div>
      </form>
    </div>
  );
}
