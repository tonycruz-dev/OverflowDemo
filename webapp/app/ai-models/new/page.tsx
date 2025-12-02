import AiModelForm from "../components/AiModelForm";

export default function NewAiModelPage() {
  return (
    <div className="max-w-3xl mx-auto px-6 py-8 space-y-6">
      <h1 className="text-2xl font-semibold">Create AI Model</h1>
      <AiModelForm mode="create" />
    </div>
  );
}
