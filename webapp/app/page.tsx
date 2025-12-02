import { AcademicCapIcon } from "@heroicons/react/24/solid";
import { getAIModel } from "@/lib/actions/ai-model-action";
import { AIModel } from "@/lib/types";

export default async function Home() {
  const { data: models, error } = await getAIModel();
  const list: AIModel[] = models ?? [];

  return (
    <main className="flex min-h-[calc(100vh-160px)] items-center justify-center px-6 py-10">
      <div className="w-full max-w-6xl mx-auto flex flex-col gap-12">
        {/* Hero */}
        <section className="flex flex-col md:flex-row items-center gap-10">
          <div className="flex flex-col items-center md:items-start gap-5 text-secondary">
            <AcademicCapIcon className="h-40 w-40 md:h-56 md:w-56" />
            <div className="text-center md:text-left">
              <h1 className="text-4xl md:text-5xl font-extrabold tracking-tight uppercase">
                Overflow{" "}
                <span className="block text-sm md:text-base mt-1 font-semibold tracking-[0.25em] text-neutral-500 dark:text-neutral-400">
                  powered by <span className="text-secondary">AI</span>
                </span>
              </h1>
            </div>
          </div>

          <div className="flex-1 space-y-4">
            <h2 className="text-2xl md:text-3xl font-semibold">
              Welcome to Overflow!
            </h2>
            <p className="text-base md:text-lg text-neutral-600 dark:text-neutral-300">
              Overflow is your AI-augmented developer Q&amp;A hub. Ask
              real-world questions about your codebase, frameworks, APIs, or
              architecture and get answers that are:
            </p>
            <ul className="list-disc list-inside space-y-1 text-neutral-600 dark:text-neutral-300">
              <li>Grounded in your actual error messages and context.</li>
              <li>
                Structured and actionable (diagnosis, root cause, and fix).
              </li>
              <li>Backed by multiple AI models working together.</li>
            </ul>
            <p className="text-sm md:text-base text-neutral-500 dark:text-neutral-400">
              Under the hood, Overflow routes each question to a curated set of
              AI models and records their answers, so you can compare responses,
              vote on what works, and build a growing knowledge base for your
              team.
            </p>
          </div>
        </section>

        {/* Models grid */}
        <section className="space-y-4">
          <div className="flex items-center justify-between gap-4 flex-wrap">
            <h3 className="text-xl md:text-2xl font-semibold">
              AI models behind Overflow
            </h3>
            <p className="text-sm text-neutral-500 dark:text-neutral-400 max-w-xl">
              Overflow doesn&apos;t rely on a single model. It orchestrates
              several providers so you get diverse perspectives, better
              debugging help, and more reliable answers.
            </p>
          </div>

          {error && (
            <div className="text-sm text-danger">
              Failed to load models: {error.message}
            </div>
          )}

          <div className="grid gap-4 md:gap-6 md:grid-cols-2 lg:grid-cols-3">
            {list.length === 0 && !error && (
              <div className="col-span-full text-sm text-neutral-500 dark:text-neutral-400">
                No AI models available yet.
              </div>
            )}
            {list.map((model) => (
              <div
                key={model.id}
                className="rounded-2xl border border-neutral-200 dark:border-neutral-800 p-4 bg-white/70 dark:bg-neutral-900/70 shadow-sm"
              >
                <div className="text-xs font-semibold uppercase tracking-wide text-secondary mb-1">
                  {model.role || "Model"}
                </div>
                <div className="text-base font-semibold mb-2 break-all">
                  {model.name}
                </div>
                <p className="text-sm text-neutral-600 dark:text-neutral-300">
                  {model.description || "No description provided."}
                </p>
              </div>
            ))}
          </div>
        </section>
      </div>
    </main>
  );
}
