﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notes2021Blazor.Server.Controllers;
using Notes2021Blazor.Shared;

namespace Notes2021Blazor.Server.Services
{
    public class LinkProcessor
    {
        private readonly NotesDbContext db;

        public LinkProcessor(NotesDbContext context)
        {
            db = context;
        }


        public async Task<string> ProcessLinkAction(long linkId)
        {
            LinkQueue q;
            try
            {
                q = db.LinkQueue.SingleOrDefault(p => p.Id == linkId);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            if (q == null)
            {
                return "Job not in Queue";
            }

            NoteFile notefile = db.NoteFile.SingleOrDefault(p => p.Id == q.LinkedFileId);
            string notefilename = notefile.NoteFileName;

            HttpClient MyClient = new HttpClient
            {
                BaseAddress = new Uri(q.BaseUri)
            };


            switch (q.Activity)
            {
                case LinkAction.CreateBase:
                    // create base note
                    LinkCreateModel inputModel = new LinkCreateModel();

                    inputModel.linkedfile = notefilename;
                    inputModel.Secret = q.Secret;

                    inputModel.header = (db.NoteHeader.SingleOrDefault(p => p.LinkGuid == q.LinkGuid)).CloneForLink();
                    inputModel.content = (db.NoteContent.SingleOrDefault(p => p.NoteHeaderId == inputModel.header.Id)).CloneForLink();
                    try
                    {
                        inputModel.tags = Tags.CloneForLink(await db.Tags.Where(p =>
                            p.NoteFileId == notefile.Id && p.NoteHeaderId == inputModel.header.Id)
                            .ToListAsync());
                    }
                    catch
                    {
                        inputModel.tags = null;
                    }

                    inputModel.header.Id = 0;

                    HttpResponseMessage resp;
                    try
                    {
                        resp = MyClient.PostAsync("api/ApiLink",
                                new ObjectContent(typeof(LinkCreateModel), inputModel, new JsonMediaTypeFormatter()))
                            .GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                    string result = await resp.Content.ReadAsStringAsync();

                    LinkLog ll = new LinkLog()
                    {
                        EventType = "SendCreateNote",
                        EventTime = DateTime.UtcNow,
                        Event = result
                    };

                    db.LinkLog.Add(ll);

                    if (result == "Ok")
                        db.LinkQueue.Remove(q);

                    await db.SaveChangesAsync();


                    return result;

                case LinkAction.CreateResponse:
                    // create response note
                    LinkCreateRModel inputModel2 = new LinkCreateRModel();

                    inputModel2.linkedfile = notefilename;
                    inputModel2.Secret = q.Secret;

                    inputModel2.header = (await db.NoteHeader.SingleAsync(p => p.LinkGuid == q.LinkGuid)).CloneForLinkR();
                    inputModel2.content = (await db.NoteContent.SingleAsync(p => p.NoteHeaderId == inputModel2.header.Id)).CloneForLink();
                    try
                    {
                        inputModel2.tags = Tags.CloneForLink(await db.Tags.Where(p =>
                            p.NoteFileId == notefile.Id && p.NoteHeaderId == inputModel2.header.Id)
                            .ToListAsync());
                    }
                    catch
                    {
                        inputModel2.tags = null;
                    }

                    NoteHeader basehead = await NoteDataManager.GetBaseNoteHeader(db, inputModel2.header.Id);
                    inputModel2.baseGuid = basehead.LinkGuid;

                    inputModel2.header.Id = 0;

                    HttpResponseMessage resp2;
                    try
                    {
                        resp2 = MyClient.PostAsync("api/ApiLinkR",
                                new ObjectContent(typeof(LinkCreateRModel), inputModel2, new JsonMediaTypeFormatter()))
                            .GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                    string result2 = resp2.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    LinkLog ll2 = new LinkLog()
                    {
                        EventType = "SendRespNote",
                        EventTime = DateTime.UtcNow,
                        Event = result2
                    };

                    db.LinkLog.Add(ll2);

                    if (result2 == "Ok")
                        db.LinkQueue.Remove(q);

                    await db.SaveChangesAsync();


                    return result2;

                case LinkAction.Edit:


                    LinkCreateEModel model = new LinkCreateEModel()
                    {
                        tags = string.Empty,
                        linkedfile = notefilename,
                        myGuid = q.LinkGuid,
                        Secret = q.Secret
                    };

                    model.header = (await db.NoteHeader.SingleAsync(p => p.LinkGuid == q.LinkGuid)).CloneForLinkR();
                    model.content = (await db.NoteContent.SingleAsync(p => p.NoteHeaderId == model.header.Id)).CloneForLink();

                    List<Tags> myTags;

                    try
                    {
                        myTags = await db.Tags.Where(p =>
                            p.NoteFileId == notefile.Id && p.NoteHeaderId == model.header.Id).ToListAsync();

                        if (myTags == null || myTags.Count < 1)
                        {
                            model.tags = string.Empty;
                        }
                        else
                        {
                            foreach (var tag in myTags)
                            {
                                model.tags += tag.Tag + " ";
                            }

                            model.tags.TrimEnd(' ');
                        }

                    }
                    catch
                    {
                        model.tags = string.Empty;
                    }

                    model.header.Id = 0;

                    HttpResponseMessage resp3;
                    try
                    {
                        resp3 = MyClient.PutAsync("api/ApiLink",
                                new ObjectContent(typeof(LinkCreateEModel), model, new JsonMediaTypeFormatter()))
                            .GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        return ex.Message;
                    }
                    string result3 = resp3.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    LinkLog ll3 = new LinkLog()
                    {
                        EventType = "EditNote",
                        EventTime = DateTime.UtcNow,
                        Event = result3
                    };

                    db.LinkLog.Add(ll3);

                    if (result3 == "Ok")
                        db.LinkQueue.Remove(q);

                    await db.SaveChangesAsync();

                    return result3;

                default:
                    return "Bad Link Activity Request";

            }

        }



        public async Task<string> ProcessLinkDelete(long linkId)
        {
            LinkQueue q;
            try
            {
                q = db.LinkQueue.SingleOrDefault(p => p.Id == linkId);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            if (q == null)
            {
                return "Job not in Queue";
            }

            //NoteFile notefile = await db.NoteFile.SingleAsync(p => p.Id == q.LinkedFileId);
            //string notefilename = notefile.NoteFileName;

            HttpClient MyClient = new HttpClient
            {
                BaseAddress = new Uri(q.BaseUri)
            };

            HttpResponseMessage resp3;

            try
            {
                resp3 = await MyClient.DeleteAsync("api/ApiLink/" + q.LinkGuid);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            string result3 = resp3.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return result3;
        }

        public async Task<bool> Test(string Uri)
        {
            HttpClient MyClient = new HttpClient
            {
                BaseAddress = new Uri(Uri)
            };

            HttpResponseMessage resp3;

            try
            {
                resp3 = await MyClient.GetAsync("api/ApiLink");
            }
            catch
            {
                return false;
            }
            string result3 = await resp3.Content.ReadAsStringAsync();

            return result3 == "Hello Notes2021";
        }

        public async Task<bool> Test2(string Uri)
        {
            string file;
            string uri;

            int index = Uri.LastIndexOf("/");

            uri = Uri.Substring(0, index - 1);
            file = Uri.Substring(index, Uri.Length - index);

            HttpClient MyClient = new HttpClient
            {
                BaseAddress = new Uri(uri)
            };

            HttpResponseMessage resp3;

            try
            {
                resp3 = await MyClient.GetAsync("api/ApiLinkR" + file);
            }
            catch
            {
                return false;
            }
            string result3 = await resp3.Content.ReadAsStringAsync();

            return result3 == "Ok";
        }
    }
}
