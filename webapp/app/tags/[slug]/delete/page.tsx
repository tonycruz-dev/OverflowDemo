import { getTag, deleteTag } from "@/lib/actions/tag-actions";
import { redirect, notFound } from "next/navigation";

interface Props {
  params: Promise<{ slug: string }>;
}

async function doDelete(formData: FormData) {
  "use server";
  const id = formData.get("id")?.toString();
  if (!id) return;
  const res = await deleteTag(id);
  if (res.error) return; // TODO surface error
  redirect("/tags");
}

export default async function DeleteTagPage({ params }: Props) {
  const { slug } = await params;
  const { data: tag } = await getTag(slug);
  if (!tag) return notFound();

  return (
    <div className="max-w-xl space-y-6 p-6">
      <h1 className="text-2xl font-semibold">Delete Tag</h1>
      <p>
        Are you sure you want to delete the tag <strong>{tag.name}</strong>?
        This cannot be undone.
      </p>
      {tag.usageCount > 0 && (
        <p className="text-red-600 text-sm">
          Cannot delete: tag is used by {tag.usageCount} question(s).
        </p>
      )}
      <form action={doDelete} className="flex gap-3">
        <input type="hidden" name="id" value={tag.id} />
        <button
          type="submit"
          disabled={tag.usageCount > 0}
          className="bg-red-600 disabled:bg-red-300 text-white px-4 py-2 rounded hover:bg-red-700"
        >
          Delete
        </button>
        <a href={`/tags/${tag.slug}/edit`} className="px-4 py-2 rounded border">
          Back
        </a>
        <a href="/tags" className="px-4 py-2 rounded border">
          Cancel
        </a>
      </form>
    </div>
  );
}
