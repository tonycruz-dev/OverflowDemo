"use client";
import React, { useState, useTransition } from "react";
import { createAIModel, saveAIModel } from "@/lib/actions/ai-model-action";
import { AIModel } from "@/lib/types";
import { useRouter } from "next/navigation";

interface Props {
  mode: "create" | "edit";
  model?: AIModel;
}

export default function AiModelForm({ mode, model }: Props) {
  const [name, setName] = useState(model?.name || "");
  const [description, setDescription] = useState(model?.description || "");
  const [version, setVersion] = useState(model?.version || "");
  const [role, setRole] = useState(model?.role || "");
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();
  const router = useRouter();

  const disabled = isPending || !name || !version || !role;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    startTransition(async () => {
      try {
        if (mode === "create") {
          await createAIModel({ name, description, version, role });
        } else if (model) {
          await saveAIModel(model.id, { name, description, version, role });
        }
        router.push("/ai-models");
        router.refresh();
      } catch (err: unknown) {
        setError(err instanceof Error ? err.message : "Save failed");
      }
    });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5 max-w-xl">
      <div className="space-y-1">
        <label className="text-sm font-medium">Name</label>
        <input
          className="w-full rounded-md border px-3 py-2 bg-white dark:bg-neutral-900 border-neutral-300 dark:border-neutral-700 text-sm"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
        />
      </div>
      <div className="space-y-1">
        <label className="text-sm font-medium">Version</label>
        <input
          className="w-full rounded-md border px-3 py-2 bg-white dark:bg-neutral-900 border-neutral-300 dark:border-neutral-700 text-sm"
          value={version}
          onChange={(e) => setVersion(e.target.value)}
          required
        />
      </div>
      <div className="space-y-1">
        <label className="text-sm font-medium">Role</label>
        <input
          className="w-full rounded-md border px-3 py-2 bg-white dark:bg-neutral-900 border-neutral-300 dark:border-neutral-700 text-sm"
          value={role}
          onChange={(e) => setRole(e.target.value)}
          placeholder="e.g. reasoning, coding, search"
          required
        />
      </div>
      <div className="space-y-1">
        <label className="text-sm font-medium">Description</label>
        <textarea
          className="w-full rounded-md border px-3 py-2 bg-white dark:bg-neutral-900 border-neutral-300 dark:border-neutral-700 text-sm"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={4}
        />
      </div>
      {error && <div className="text-sm text-danger">{error}</div>}
      <div className="flex gap-3">
        <button
          type="submit"
          disabled={disabled}
          className="px-4 py-2 rounded-md bg-secondary text-white text-sm font-semibold disabled:opacity-50"
        >
          {isPending ? "Saving..." : mode === "create" ? "Create" : "Update"}
        </button>
        <button
          type="button"
          onClick={() => router.push("/ai-models")}
          className="px-4 py-2 rounded-md border text-sm font-medium"
          disabled={isPending}
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
