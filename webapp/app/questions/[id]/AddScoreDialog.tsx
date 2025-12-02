"use client";

import { useState } from "react";
import {
  Button,
  Modal,
  ModalBody,
  ModalContent,
  ModalFooter,
  ModalHeader,
  useDisclosure,
  RadioGroup,
  VisuallyHidden,
  useRadio,
} from "@heroui/react";
import { useRouter } from "next/navigation";
import { updateAiAnswer } from "@/lib/actions/question-actions";
import  {addVoteAi} from "@/lib/actions/profile-actions"

// lightweight cn helper (only used locally for CustomRadio styling)
const cn = (...classes: Array<string | undefined | null | false>) =>
  classes.filter(Boolean).join(" ");

// CustomRadio component based on provided example
interface CustomRadioProps {
  value: string;
  children?: React.ReactNode;
  description?: string;
}
const CustomRadio: React.FC<CustomRadioProps> = (props) => {
  const {
    Component,
    children,
    description,
    getBaseProps,
    getWrapperProps,
    getInputProps,
    getLabelProps,
    getLabelWrapperProps,
    getControlProps,
  } = useRadio(props);

  return (
    <Component
      {...getBaseProps()}
      className={cn(
        "group inline-flex items-center hover:opacity-70 active:opacity-50 justify-between flex-row-reverse tap-highlight-transparent",
        "max-w-[300px] cursor-pointer border-2 border-default rounded-lg gap-4 p-3",
        "data-[selected=true]:border-primary data-[selected=true]:bg-primary/5"
      )}
    >
      <VisuallyHidden>
        <input {...getInputProps()} />
      </VisuallyHidden>
      <span {...getWrapperProps()}>
        <span {...getControlProps()} />
      </span>
      <div {...getLabelWrapperProps()}>
        {children && <span {...getLabelProps()}>{children}</span>}
        {description && (
          <span className="text-xs text-foreground opacity-70">
            {description}
          </span>
        )}
      </div>
    </Component>
  );
};

type Props = {
  questionId: string;
  aiAnswerId: string;
  initialVotes?: number | null;
  initialHelpful?: number | null;
  initialNotHelpful?: number | null;
  onUpdated?: () => void; // optional callback after success
  model?: string; // added optional model name
  // new optional controlled props
  isOpen?: boolean;
  onClose?: () => void;
  // voting identity info
  userId?: string;
  existingVotedByUserIds?: string[];
};

export default function AddScoreDialog({
  questionId,
  aiAnswerId,
  initialVotes = 0,
  initialHelpful = 0,
  initialNotHelpful = 0,
  onUpdated,
  model, // destructure model
  isOpen: controlledIsOpen,
  onClose: controlledOnClose,
  userId,
  existingVotedByUserIds = [],
}: Props) {
  // internal disclosure (used only if uncontrolled)
  const internalDisclosure = useDisclosure();
  const isControlled = typeof controlledIsOpen === "boolean";
  const isOpen = isControlled ? controlledIsOpen! : internalDisclosure.isOpen;
  const onOpen = internalDisclosure.onOpen; // only used when uncontrolled
  const onClose = () => {
    if (isControlled) {
      controlledOnClose?.();
    } else {
      internalDisclosure.onClose();
    }
  };
  const onOpenChange = internalDisclosure.onOpenChange;

  const router = useRouter();

  // store numbers (not strings)
  const [votes, setVotes] = useState<number>(initialVotes ?? 0);
  const [helpful, setHelpful] = useState<number>(initialHelpful ?? 0);
  const [notHelpful, setNotHelpful] = useState<number>(initialNotHelpful ?? 0);
  const [submitting, setSubmitting] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const submit = async () => {
    setSubmitting(true);
    setErr(null);
    try {
      const updatedVoters =
        existingVotedByUserIds.includes(userId || "") || !userId
          ? existingVotedByUserIds
          : [...existingVotedByUserIds, userId];
      const dto = {
        votes,
        userHelpfulVotes: helpful,
        userNotHelpfulVotes: notHelpful,
        votedByUserIds: updatedVoters,
      };

      const res = await updateAiAnswer(questionId, aiAnswerId, dto);
      
      const resVote = await addVoteAi({
        questionId,
        aiId: aiAnswerId,
        targetId: aiAnswerId,
        targetType: "Answer",
        voteValue: votes
      });

      if (res.error) {
        throw new Error(res.error.message || `Failed (${res.error.status})`);
      }
      if (resVote.error) {
        throw new Error(resVote.error.message || `Failed (${resVote.error.status})`);
      }

      onClose();
      onUpdated?.();
      router.refresh();
    } catch (e) {
      const message = e instanceof Error ? e.message : "Something went wrong";
      setErr(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      {/* Trigger button only when uncontrolled */}
      {!isControlled && (
        <Button color="warning" size="sm" variant="flat" onPress={onOpen}>
          Add Score
        </Button>
      )}

      <Modal
        isOpen={isOpen}
        onOpenChange={onOpenChange}
        size="2xl"
        backdrop="blur"
      >
        <ModalContent>
          {() => (
            <>
              <ModalHeader className="flex flex-col gap-1">
                Provide the score for ({model})
              </ModalHeader>
              <ModalBody className="gap-6">
                {/* Votes Group */}
                <RadioGroup
                  label="Votes"
                  value={String(votes)}
                  onValueChange={(v) => setVotes(Number(v))}
                  orientation="horizontal"
                  className="flex flex-wrap gap-3"
                >
                  <CustomRadio value="0" description="No score">
                    0 – No score
                  </CustomRadio>
                  <CustomRadio value="5">5</CustomRadio>
                  <CustomRadio value="10">10</CustomRadio>
                  <CustomRadio value="15">15</CustomRadio>
                  <CustomRadio value="20" description="Excellent">
                    20 – Excellent
                  </CustomRadio>
                </RadioGroup>

                {/* Helpful Group */}
                <RadioGroup
                  label="Helpful score"
                  value={String(helpful)}
                  onValueChange={(v) => setHelpful(Number(v))}
                  orientation="horizontal"
                  className="flex flex-wrap gap-3"
                >
                  <CustomRadio value="0" description="No helpful votes">
                    0 – No score
                  </CustomRadio>
                  <CustomRadio value="5">5</CustomRadio>
                  <CustomRadio value="10">10</CustomRadio>
                  <CustomRadio value="15">15</CustomRadio>
                  <CustomRadio value="20" description="Top helpful">
                    20 – Excellent
                  </CustomRadio>
                </RadioGroup>

                {/* Not Helpful Group */}
                <RadioGroup
                  label="Not helpful score"
                  value={String(notHelpful)}
                  onValueChange={(v) => setNotHelpful(Number(v))}
                  orientation="horizontal"
                  className="flex flex-wrap gap-3"
                >
                  <CustomRadio value="0" description="Not negative">
                    0 – Not negative
                  </CustomRadio>
                  <CustomRadio value="-5" description="Slightly negative">
                    -5
                  </CustomRadio>
                  <CustomRadio value="-10" description="Moderately negative">
                    -10
                  </CustomRadio>
                  <CustomRadio value="-15" description="Very negative">
                    -15
                  </CustomRadio>
                  <CustomRadio value="-20" description="Extremely negative">
                    -20
                  </CustomRadio>
                </RadioGroup>

                {err && <div className="text-danger text-sm">{err}</div>}
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
                  isLoading={submitting}
                  isDisabled={submitting}
                >
                  {submitting ? "Saving…" : "Save"}
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>
    </>
  );
}
