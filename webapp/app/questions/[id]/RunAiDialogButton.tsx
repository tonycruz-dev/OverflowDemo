"use client";

import React, { useEffect, useMemo, useState, useTransition } from "react";
import {
  Button,
  Modal,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  useDisclosure,
  CheckboxGroup,
  Checkbox,
} from "@heroui/react";
import { usePathname, useRouter } from "next/navigation";
import { PostAiAnswersOpts, AIModel, FetchResponse } from "@/lib/types"; // added FetchResponse
import { AiAnswer } from "./AiAnswerGroup";

export type AiAnswerLite = {
  aiModel: string; // from your existing AnswerByAI entities
};

// NOTE: The hardcoded ALL_MODELS list has been removed. Supply aiModels prop from a parent SERVER component that calls getAIModel() from lib/actions/ai-model-action.ts.
// Parent example (server component):
// import { getAIModel } from "@/lib/actions/ai-model-action";
// const { data: aiModels } = await getAIModel();
// <RunAiDialogButton aiModels={aiModels ?? []} ... />

// helpers to canonicalize now use incoming aiModels
const toKey = (s: string) => s.trim().toLowerCase();

interface Props {
  questionId: string;
  existingAnswers?: AiAnswerLite[];
  mode?: "hide" | "disable";
  defaultModels?: string[];
  runAi: (opts: PostAiAnswersOpts) => Promise<FetchResponse<AiAnswer[]>>; // refined type
  aiModels?: AIModel[]; // made optional
}

const canonicalizeFactory = (models: AIModel[]) => {
  // Build lookup: canonical id -> aliases (id + name + lower variants)
  const map: Record<string, string[]> = {};
  for (const m of models) {
    const aliases = [m.id, m.name].filter(Boolean);
    map[m.id] = aliases;
  }
  return (model: string): string | null => {
    const m = toKey(model);
    for (const key of Object.keys(map)) {
      const aliases = map[key].map(toKey);
      if (aliases.includes(m) || toKey(key) === m) return key;
    }
    return null;
  };
};

export default function RunAiDialogButton({
  questionId,
  existingAnswers = [],
  mode = "hide",
  defaultModels,
  runAi,
  aiModels = [], // default safeguard
}: Props) {
  const { isOpen, onOpen, onOpenChange, onClose } = useDisclosure();
  const router = useRouter();
  const pathname = usePathname();
  const [isPending, startTransition] = useTransition();

  // Build canonical model list from aiModels
  const allCanonical = useMemo(
    () => (aiModels ?? []).map((m) => m.id),
    [aiModels]
  );
  const canonicalize = useMemo(
    () => canonicalizeFactory(aiModels ?? []),
    [aiModels]
  );

  // set of already-run canonical model ids
  const alreadyRunSet = useMemo(() => {
    const set = new Set<string>();
    for (const a of existingAnswers) {
      const key = canonicalize(a.aiModel);
      if (key) set.add(key);
    }
    return set;
  }, [existingAnswers, canonicalize]);

  // selectable list
  const selectable = useMemo(
    () =>
      mode === "hide"
        ? allCanonical.filter((k) => !alreadyRunSet.has(k))
        : allCanonical,
    [mode, alreadyRunSet, allCanonical]
  );

  const computedDefault = useMemo(() => {
    const base = (
      defaultModels && defaultModels.length
        ? defaultModels
        : selectable.slice(0, 2)
    ) as string[];
    return base.filter(
      (id) => selectable.includes(id) && !alreadyRunSet.has(id)
    );
  }, [defaultModels, selectable, alreadyRunSet]);
  const [models, setModels] = useState<string[]>(computedDefault);
  useEffect(() => {
    if (isOpen) setModels(computedDefault);
  }, [isOpen, computedDefault]);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const filteredSelected = useMemo(
    () =>
      models.filter((id) => selectable.includes(id) && !alreadyRunSet.has(id)),
    [models, selectable, alreadyRunSet]
  );

  const submit = async () => {
    setSubmitting(true);
    setErrorMsg(null);
    try {
      // Only send currently selected models that haven't already run
      const selectedIds = Array.from(new Set(filteredSelected));
      const selectedModelNames = selectedIds.map(
        (id) => aiModels.find((m) => m.id === id)?.name || id
      );
      const opts: PostAiAnswersOpts = { include: selectedModelNames };
      const res = await runAi(opts);
      if (res?.error) throw new Error(res.error.message ?? "Request failed");
      onClose();
      const path = `/questions/${questionId}`;
      if (pathname !== path) {
        router.push(path);
      }
      startTransition(() => {
        router.refresh();
      });
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : "Something went wrong";
      setErrorMsg(message);
    } finally {
      setSubmitting(false);
    }
  };

  const noModels = aiModels.length === 0;
  const allDone = !noModels && selectable.length === 0; // only "done" if we actually had models

  return (
    <>
      <Button color="secondary" size="sm" onPress={onOpen} isDisabled={allDone}>
        {noModels
          ? "No AI models available"
          : allDone
          ? "All models already ran"
          : "Run AI model for answer"}
      </Button>
      <Modal
        isOpen={isOpen}
        onOpenChange={onOpenChange}
        size="lg"
        backdrop="blur"
      >
        <ModalContent>
          {() => (
            <>
              <ModalHeader className="flex flex-col gap-1">
                Run AI model for answer
              </ModalHeader>
              <ModalBody className="gap-6">
                {mode === "disable" && alreadyRunSet.size > 0 && (
                  <div className="text-small text-foreground-500">
                    Models already run will be disabled.
                  </div>
                )}
                <div className="space-y-2">
                  <div className="text-sm text-foreground-500">
                    Select which models to run:
                  </div>
                  <CheckboxGroup
                    value={models}
                    onValueChange={(v) => setModels(v as string[])}
                    orientation="vertical"
                  >
                    {selectable.map((key) => (
                      <Checkbox
                        key={key}
                        value={key}
                        isDisabled={
                          mode === "disable" && alreadyRunSet.has(key)
                        }
                      >
                        {(aiModels ?? []).find((m) => m.id === key)?.name ||
                          key}
                      </Checkbox>
                    ))}
                    {mode === "disable" &&
                      allCanonical
                        .filter((k) => !selectable.includes(k))
                        .map((k) => (
                          <Checkbox key={k} value={k} isDisabled>
                            {((aiModels ?? []).find((m) => m.id === k)?.name ||
                              k) + " (already run)"}
                          </Checkbox>
                        ))}
                  </CheckboxGroup>
                </div>
                {errorMsg && (
                  <div className="text-danger text-sm">{errorMsg}</div>
                )}
              </ModalBody>
              <ModalFooter>
                <Button
                  variant="flat"
                  onPress={onClose}
                  isDisabled={submitting}
                >
                  Cancel
                </Button>
                <Button
                  color="primary"
                  onPress={submit}
                  isLoading={submitting || isPending}
                  isDisabled={
                    !submitting && !isPending && filteredSelected.length === 0
                  }
                >
                  {submitting || isPending ? "Loading" : "Run"}
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>
    </>
  );
}
