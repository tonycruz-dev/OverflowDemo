export type PaginatedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
};
export type QuestionParams = {
  tag?: string;
  page?: number;
  pageSize?: number;
  sort?: string;
};
export type Question = {
  id: string;
  title: string;
  content: string;
  askerId: string;
  author?: Profile;
  createdAt: string;
  updatedAt?: string;
  viewCount: number;
  tagSlugs: string[];
  hasAcceptedAnswer: boolean;
  votes: number;
  answerCount: number;
  answers: Answer[];
  userVoted: number;
  aiAnswers: AiAnswer[]
};

export type Answer = {
  id: string;
  content: string;
  userId: string;
  author?: Profile;
  createdAt: string;
  updatedAt?: string;
  accepted: boolean;
  questionId: string;
  votes: number;
  userVoted: number;
};

export type Tag = {
  id: string;
  name: string;
  slug: string;
  description: string;
  usageCount: number;
};

export type TrendingTag = {
  tag: string;
  count: number;
};

export type Profile = {
  userId: string;
  displayName: string;
  description?: string;
  reputation: number;
};

export type FetchResponse<T> = {
  data: T | null;
  error?: { message: string; status: number };
};

export type VoteRecord = {
  targetId: string;
  targetType: "Question" | "Answer";
  voteValue: number;
};

export type Vote = {
  targetId: string;
  targetType: "Question" | "Answer";
  targetUserId: string;
  questionId: string;
  voteValue: 1 | -1;
};

export type TopUser = {
  userId: string;
  delta: number;
};
export interface AiAnswer {
  id: string;
  content: string;
  aiModel: string;
  confidenceScore: number;
  createdAt: string;
  updatedAt: string;
  votes: number;
  userHelpfulVotes: number;
  userNotHelpfulVotes: number;
  rawAiResponse: string;
  promptUsed: string;
  diagnosis: string;
  likelyRootCause: string;
  fixStepByStep: string;
  codePatch: string;
  alternatives: string;
  gotchas: string;
  questionId: string;
  accepted: boolean;
  userId: string;
  hasVoted: boolean;
}

export type PostAiAnswersOpts = {
  include: string[];           // models to run
  maxChars?: number;           // optional (server defaults to 5000)
  alreadyRunModels?: string[]; // optional: prevent duplicate runs
};

export type TopUserWithProfile = TopUser & { profile: Profile };

export type CastVoteAiDto = {
  questionId: string;
  aiId: string;                       // AnswerByAI Id (or model-run Id)
  targetId: string;                   // The Question/Answer being voted on
  targetType: "Question" | "Answer";  // must match your API constraint
  voteValue: number;                  // upvote or downvote
};

export type UserVotesResult = {
  aiId: string;
  questionId: string;
  userId: string;
  targetId: string;
  targetType: "Question" | "Answer";
  voteValue: number; // 1 or -1
};

export type TopAiVote = {
  aiModel: string;
  totalVotes: number; 
};
export type AIModel = {
  id: string;
  name: string;
  description: string;
  version: string;
  role: string;
}