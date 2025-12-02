"use client";
import React, { useTransition, useState } from "react";
import Link from "next/link";
import { AIModel } from "@/lib/types";
import { removeAIModel } from "@/lib/actions/ai-model-action";
import {
  PencilSquareIcon,
  TrashIcon,
  PlusIcon,
} from "@heroicons/react/24/outline";
import { User } from "next-auth";

interface Props {
  initialModels: AIModel[];
  currentUser: User | null; // Fixed type from Use<User> to User
}

export default function AiModelsManager({ initialModels, currentUser }: Props) {
  const [models, setModels] = useState<AIModel[]>(initialModels);
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  const isAuthed = !!currentUser;

  const handleDelete = async (id: string) => {
    if (!confirm("Delete this model?")) return;
    setError(null);
    startTransition(async () => {
      try {
        await removeAIModel(id);
        setModels((prev) => prev.filter((m) => m.id !== id));
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : "Delete failed");
      }
    });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        {isAuthed && (
          <Link
            href="/ai-models/new"
            className="inline-flex items-center gap-2 px-3 py-2 rounded-md bg-secondary text-white text-sm font-medium hover:opacity-90"
          >
            <PlusIcon className="h-4 w-4" />
            New Model
          </Link>
        )}
        {isPending && (
          <span className="text-xs text-neutral-500">Working...</span>
        )}
        {error && <span className="text-xs text-danger">{error}</span>}
      </div>

      {/* Models grid */}
      <div className="grid gap-4 md:gap-6 md:grid-cols-2 lg:grid-cols-3">
        {models.length === 0 && (
          <div className="col-span-full text-sm text-neutral-500 dark:text-neutral-400 text-center py-6 border rounded-xl border-dashed border-neutral-300 dark:border-neutral-700">
            No models.
          </div>
        )}
        {models.map((m) => (
          <div
            key={m.id}
            className="group rounded-2xl border border-neutral-200 dark:border-neutral-800 p-4 bg-white/70 dark:bg-neutral-900/70 shadow-sm relative flex flex-col gap-2"
          >
            <div className="flex items-start justify-between gap-2">
              <div className="flex-1 min-w-0">
                <div className="text-xs font-semibold uppercase tracking-wide text-secondary mb-1">
                  {m.role || "Model"}
                </div>
                <div
                  className="text-base font-semibold break-all"
                  title={m.name}
                >
                  {m.name}
                </div>
              </div>
              {isAuthed && (
                <div className="flex items-center gap-2 opacity-70 group-hover:opacity-100 transition">
                  <Link
                    href={`/ai-models/${encodeURIComponent(m.id)}/edit`}
                    aria-label={`Edit ${m.name}`}
                    className="p-1 rounded hover:bg-neutral-100 dark:hover:bg-neutral-800"
                  >
                    <PencilSquareIcon className="h-5 w-5 text-primary" />
                  </Link>
                  <button
                    onClick={() => handleDelete(m.id)}
                    aria-label={`Delete ${m.name}`}
                    className="p-1 rounded hover:bg-neutral-100 dark:hover:bg-neutral-800"
                    disabled={isPending}
                  >
                    <TrashIcon className="h-5 w-5 text-danger" />
                  </button>
                </div>
              )}
            </div>
            {m.version && (
              <div className="text-[10px] font-medium tracking-wide text-neutral-500 dark:text-neutral-400">
                v{m.version}
              </div>
            )}
            <p
              className="text-sm text-neutral-600 dark:text-neutral-300 line-clamp-4"
              title={m.description}
            >
              {m.description || "No description provided."}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}
