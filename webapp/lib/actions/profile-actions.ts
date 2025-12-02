"use server";

import { fetchClient } from "@/lib/fetchClient";
import { CastVoteAiDto, FetchResponse, Profile, TopUser, TopUserWithProfile, UserVotesResult } from "@/lib/types";
import { revalidatePath } from "next/cache";
import { EditProfileSchema } from "@/lib/schemas/editProfileSchema";

export async function getUserProfiles(sortBy?: string) {
  let url = "/profiles";
  if (sortBy) url += "?sortBy=" + sortBy;
  return fetchClient<Profile[]>(url, "GET");
}

export async function getProfileById(id: string) {
    debugger
  return fetchClient<Profile>(`/profiles/${id}`, "GET");
}

export async function editProfile(id: string, profile: EditProfileSchema) {
  const result = await fetchClient<Profile>(`/profiles/edit`, "PUT", {
    body: profile,
  });

  revalidatePath(`/profiles/${id}`);

  return result;
}
export async function getTopUsers(): Promise<FetchResponse<TopUserWithProfile[]>> {
  const { data: users, error } = await fetchClient<TopUser[]>(
        "/stats/top-users",
        "GET",
        {
          cache: "force-cache",
          next: { revalidate: 3600 },
        }
      );
  // If the endpoint isn't available or returns 404, treat as no top users
  if (error?.status === 404 || !users?.length) {
    return { data: [] as TopUserWithProfile[] };
  }
  if (error) return { data: null, error };

  const ids = [...new Set(users.map((u) => u.userId))];
  const qs = encodeURIComponent(ids.join(","));

  // If no IDs, short-circuit
  if (ids.length === 0) return { data: [] as TopUserWithProfile[] };

  const { data: profiles, error: profilesError } = await fetchClient<Profile[]>(
    `/profiles/batch?ids=${qs}`,
    "GET",
    { cache: "force-cache", next: { revalidate: 3600 } }
  );

  if (profilesError)
    return {
      data: null,
      error: { message: "Problem getting profiles", status: 500 },
    };

  const byId = new Map((profiles ?? []).map((p) => [p.userId, p]));

  return {
    data: users.map((u) => ({
      ...u,
      profile: byId.get(u.userId),
    })) as TopUserWithProfile[],
  };
}
/**
 * POST /votesAi
 * Cast a vote related to an AI answer (up/down on a Question/Answer).
 * - Revalidates the question page on success.
 */
export async function addVoteAi(dto: CastVoteAiDto): Promise<FetchResponse<void>> {
  const result = await fetchClient<void>("/votesAi", "POST", { body: dto });

  // Keep the question page fresh
  revalidatePath(`/questions/${dto.questionId}`);

  return result;
}
/**
 * GET /votesAi/{questionId}/{aiId}
 * Returns all votes for a specific AI answer within a question.
 * (Your API requires auth; ensure your fetchClient forwards cookies/headers.)
 */
export async function getVotesAi(questionId: string, aiId: string): Promise<FetchResponse<UserVotesResult[]>> {
  return fetchClient<UserVotesResult[]>(
    `/votesAi/${encodeURIComponent(questionId)}/${encodeURIComponent(aiId)}`,
    "GET"
  );
}

/**
 * GET /votesAi/{questionId}
 * Returns all votes for a specific AI answer within a question.
 * (Your API requires auth; ensure your fetchClient forwards cookies/headers.)
 */
export async function fetchUserVotesByQuestionId(questionId: string): Promise<FetchResponse<UserVotesResult[]>> {
  return fetchClient<UserVotesResult[]>(
    `/votesais/${encodeURIComponent(questionId)}`,
    "GET"
  );
}
