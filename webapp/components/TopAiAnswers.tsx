import { getTopAiAnswers } from "@/lib/actions/question-actions";

export default async function TopAiAnswers() {
  const { data: aiAnswers, error } = await getTopAiAnswers();
  //if (!users || users.length === 0) return null;

  return (
    <div className="bg-primary-50 p-6 rounded-2xl">
      <h3 className="text-2xl text-secondary mb-5 text-center">
        AI Performance Leaderboards
      </h3>
      <div className="flex flex-col px-6 gap-3">
       {error ? (
          <div>Unavailable</div>
        ) : (
          <>
            {aiAnswers?.map((u) => (
              <div className="flex justify-between items-center" key={u.aiModel}>
                <div>{u.aiModel}</div>
                <div>{u.totalVotes}</div>
              </div>
            ))}
          </>
        )}
      </div>
    </div>
  );
}
