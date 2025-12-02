import { getAIModel } from "@/lib/actions/ai-model-action";
import AiModelsManager from "@/app/ai-models/AiModelsManager"; //"@/ ./AiModelsManager";
import { AIModel } from "@/lib/types";
import { getCurrentUser } from "@/lib/actions/auth-actions";

export default async function AiModelsPage() {
  const { data, error } = await getAIModel();
  const currentUser = (await getCurrentUser()) ?? null;
  const models: AIModel[] = data ?? [];
  return (
    <div className="max-w-5xl mx-auto px-6 py-8 space-y-6">
      <h1 className="text-2xl font-semibold">AI Models</h1>
      {error && (
        <div className="text-sm text-danger">
          Failed to load: {error.message}
        </div>
      )}
      <AiModelsManager initialModels={models} currentUser={currentUser} />
    </div>
  );
}
