import { getTag, updateTag } from "@/lib/actions/tag-actions";
import { redirect, notFound } from "next/navigation";

interface Props {
  params: Promise<{ slug: string }>;
}

async function doUpdate(formData: FormData) {
  "use server";
  const id = formData.get("id")?.toString();
  const name = formData.get("name")?.toString().trim();
  const description = formData.get("description")?.toString().trim();
  const slug = formData.get("slug")?.toString().trim();
  if (!id) return;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const dto: any = {};
  if (name) dto.name = name;
  if (description) dto.description = description;
  if (slug) dto.slug = slug;
  const res = await updateTag(id, dto);
  if (res.error) return; // TODO surface error
  redirect("/tags");
}

export default async function EditTagPage({ params }: Props) {
  const { slug } = await params;
  const { data: tag } = await getTag(slug);
  if (!tag) return notFound();

  return (
    <div className="max-w-xl space-y-6 p-6">
      <h1 className="text-2xl font-semibold">Edit Tag</h1>
      <form action={doUpdate} className="flex flex-col gap-4">
        <input type="hidden" name="id" value={tag.id} />
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Name</span>
          <input
            name="name"
            defaultValue={tag.name}
            className="border rounded px-3 py-2"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Slug</span>
          <input
            name="slug"
            defaultValue={tag.slug}
            className="border rounded px-3 py-2"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium">Description</span>
          <textarea
            name="description"
            rows={5}
            defaultValue={tag.description}
            className="border rounded px-3 py-2"
          />
        </label>
        <div className="flex gap-3">
          <button
            type="submit"
            className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700"
          >
            Save
          </button>
          <a
            href={`/tags/${tag.slug}/delete`}
            className="px-4 py-2 rounded border"
          >
            Delete
          </a>
          <a href="/tags" className="px-4 py-2 rounded border">
            Cancel
          </a>
        </div>
      </form>
    </div>
  );
}
