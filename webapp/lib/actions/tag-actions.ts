"use server";

import { fetchClient } from "@/lib/fetchClient";
import { Tag, TrendingTag } from "@/lib/types";

export async function getTags(sort?: string) {
  let url = "/tags";
  if (sort) url += "?sort=" + sort;

  return fetchClient<Tag[]>(url, "GET", {
    cache: "force-cache",
    next: { revalidate: 3600 },
  });
}

export async function getTag(slugOrId: string) {
  return fetchClient<Tag>(`/tags/${slugOrId}`, "GET", { cache: "no-store" });
}

export type CreateTagDto = {
  name: string;
  description: string;
  slug?: string; // optional custom slug
};

export async function createTag(dto: CreateTagDto) {
  return fetchClient<Tag>("/tags", "POST", { body: dto });
}

export type UpdateTagDto = {
  name?: string;
  description?: string;
  slug?: string;
};

export async function updateTag(id: string, dto: UpdateTagDto) {
  return fetchClient<void>(`/tags/${id}`, "PUT", { body: dto });
}

export async function deleteTag(id: string) {
  return fetchClient<void>(`/tags/${id}`, "DELETE");
}

export async function getTrendingTags() {
  const res = await fetchClient<TrendingTag[]>("/stats/trending-tags", "GET", {
    cache: "force-cache",
    next: { revalidate: 3600 },
  });
  if (res.error?.status === 404) return { data: [] };
  return res;
}
