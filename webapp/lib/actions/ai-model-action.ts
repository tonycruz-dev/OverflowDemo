"use server";

import { fetchClient } from "@/lib/fetchClient";
import { AIModel } from "@/lib/types";
import { revalidatePath } from "next/cache";

export async function getAIModel() {
  const url = "/aimodeles";
  return fetchClient<AIModel[]>(url, "GET");
}

export async function getAIModelById(id: string) {
  return fetchClient<AIModel>(`/aimodeles/${id}`, "GET");
}

// DTO types
export type CreateAIModelDto = Omit<AIModel, "id"> & { id?: string }; // id optional if server generates
export type UpdateAIModelDto = Partial<Omit<AIModel, "id">>; // partial update

// Add (create) a new AI model
export async function addAIModel(dto: CreateAIModelDto) {
  return fetchClient<AIModel>("/aimodeles", "POST", { body: dto });
}

// Update an existing AI model by id
export async function updateAIModel(id: string, dto: UpdateAIModelDto) {
  return fetchClient<AIModel>(`/aimodeles/${id}`, "PUT", { body: dto });
}

// Delete an AI model by id
export async function deleteAIModel(id: string) {
  return fetchClient<void>(`/aimodeles/${id}`, "DELETE");
}

export async function createAIModel(dto: CreateAIModelDto) {
  const res = await addAIModel(dto);
  revalidatePath("/ai-models");
  return res;
}

export async function saveAIModel(id: string, dto: UpdateAIModelDto) {
  const res = await updateAIModel(id, dto);
  revalidatePath("/ai-models");
  return res;
}

export async function removeAIModel(id: string) {
  const res = await deleteAIModel(id);
  revalidatePath("/ai-models");
  return res;
}
