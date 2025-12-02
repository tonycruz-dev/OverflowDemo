import { getAIModelById } from "@/lib/actions/ai-model-action";
import AiModelForm from "../../components/AiModelForm";
import { notFound } from "next/navigation";

interface Props {
  // Adjusted to align with Next.js expected PageProps where params can be a Promise
  params: Promise<{ id: string }>;
}

export default async function EditAiModelPage({ params }: Props) {
  const { id } = await params;
  const { data, error } = await getAIModelById(id);
  if (error || !data) return notFound();
  return (
    <div className="max-w-3xl mx-auto px-6 py-8 space-y-6">
      <h1 className="text-2xl font-semibold">Edit AI Model</h1>
      <AiModelForm mode="edit" model={data} />
    </div>
  );
}
