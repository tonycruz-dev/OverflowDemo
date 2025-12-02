import { getQuestionById } from "@/lib/actions/question-actions";
import { fetchUserVotesByQuestionId } from "@/lib/actions/profile-actions";
import { notFound } from "next/navigation";
import QuestionDetailedHeader from "@/app/questions/[id]/QuestionDetailedHeader";
import QuestionContent from "@/app/questions/[id]/QuestionContent";
import AnswerContent from "@/app/questions/[id]/AnswerContent";
import AnswersHeader from "@/app/questions/[id]/AnswersHeader";
import AnswerForm from "@/app/questions/[id]/AnswerForm";
import { Answer } from "@/lib/types";
import AiAnswerGroup from "./AiAnswerGroup";
import { getCurrentUser } from "@/lib/actions/auth-actions";

type Params = Promise<{ id: string }>;
type SearchParams = Promise<{ sort?: string }>;

export default async function QuestionDetailedPage({
  params,
  searchParams,
}: {
  params: Params;
  searchParams: SearchParams;
}) {
  const { id } = await params;
  const { sort } = await searchParams;
  const { data: question, error } = await getQuestionById(id);
  const userVotes = await fetchUserVotesByQuestionId(id);
  const currentUser = await getCurrentUser();

  console.log("Fetched userVotes:", userVotes);

  if (error) throw error;
  if (!question) return notFound();
  console.log(
    "QuestionDetailedPage rendering for question:",
    question.aiAnswers
  );

  const sortMode = sort === "created" ? "created" : "highScore";

  const sortHighScore = (a: Answer, b: Answer) => {
    if (a.accepted !== b.accepted) return a.accepted ? -1 : 1;
    const va = a.votes ?? 0,
      vb = b.votes ?? 0;
    if (va !== vb) return vb - va;
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
  };

  const sortCreated = (a: Answer, b: Answer) => {
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
  };

  const answers = [...question.answers].sort(
    sortMode === "created" ? sortCreated : sortHighScore
  );

  return (
    <div className="w-full">
      <QuestionDetailedHeader question={question} />
      <QuestionContent question={question} />
      {question.answers.length > 0 && (
        <AnswersHeader answerCount={question.answers.length} />
      )}
      {answers.map((answer) => (
        <AnswerContent
          answer={answer}
          key={answer.id}
          askerId={question.askerId}
        />
      ))}
      {question.aiAnswers.length > 0 && (
        <AiAnswerGroup
          key={question.aiAnswers[0].id}
          answers={question.aiAnswers}
          userVotes={userVotes.data ?? []}
          userId={currentUser?.id || ""}
        />
      )}
      <AnswerForm questionId={question.id} />
    </div>
  );
}
