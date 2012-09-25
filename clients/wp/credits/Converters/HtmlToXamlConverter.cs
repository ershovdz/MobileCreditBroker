using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Windows.Documents;

namespace Credits.Converters
{
    internal enum HtmlTokenType
    {
        OpeningTagStart,
        ClosingTagStart,
        TagEnd,
        EmptyTagEnd,
        EqualSign,
        Name,
        Atom, // any attribute value not in quotes
        Text, //text content when accepting text
        Comment,
        EOF,
    }

    internal class HtmlLexicalAnalyzer
    {
        // ---------------------------------------------------------------------
        //
        // Constructors
        //
        // ---------------------------------------------------------------------

        #region Constructors

        /// <summary>
        /// initializes the _inputStringReader member with the string to be read
        /// also sets initial values for _nextCharacterCode and _nextTokenType
        /// </summary>
        /// <param name="inputTextString">
        /// text string to be parsed for xml content
        /// </param>
        internal HtmlLexicalAnalyzer(string inputTextString)
        {
            _inputStringReader = new StringReader(inputTextString);
            _nextCharacterCode = 0;
            _nextCharacter = ' ';
            _lookAheadCharacterCode = _inputStringReader.Read();
            _lookAheadCharacter = (char)_lookAheadCharacterCode;
            _previousCharacter = ' ';
            _ignoreNextWhitespace = true;
            _nextToken = new StringBuilder(100);
            _nextTokenType = HtmlTokenType.Text;
            // read the first character so we have some value for the NextCharacter property
            this.GetNextCharacter();
        }

        #endregion Constructors

        // ---------------------------------------------------------------------
        //
        // Internal methods
        //
        // ---------------------------------------------------------------------

        #region Internal Methods

        /// <summary>
        /// retrieves next recognizable token from input string
        /// and identifies its type
        /// if no valid token is found, the output parameters are set to null
        /// if end of stream is reached without matching any token, token type
        /// paramter is set to EOF
        /// </summary>
        internal void GetNextContentToken()
        {
            Debug.Assert(_nextTokenType != HtmlTokenType.EOF);
            _nextToken.Length = 0;
            if (this.IsAtEndOfStream)
            {
                _nextTokenType = HtmlTokenType.EOF;
                return;
            }

            if (this.IsAtTagStart)
            {
                this.GetNextCharacter();

                if (this.NextCharacter == '/')
                {
                    _nextToken.Append("</");
                    _nextTokenType = HtmlTokenType.ClosingTagStart;

                    // advance
                    this.GetNextCharacter();
                    _ignoreNextWhitespace = false; // Whitespaces after closing tags are significant
                }
                else
                {
                    _nextTokenType = HtmlTokenType.OpeningTagStart;
                    _nextToken.Append("<");
                    _ignoreNextWhitespace = true; // Whitespaces after opening tags are insignificant
                }
            }
            else if (this.IsAtDirectiveStart)
            {
                // either a comment or CDATA
                this.GetNextCharacter();
                if (_lookAheadCharacter == '[')
                {
                    // cdata
                    this.ReadDynamicContent();
                }
                else if (_lookAheadCharacter == '-')
                {
                    this.ReadComment();
                }
                else
                {
                    // neither a comment nor cdata, should be something like DOCTYPE
                    // skip till the next tag ender
                    this.ReadUnknownDirective();
                }
            }
            else
            {
                // read text content, unless you encounter a tag
                _nextTokenType = HtmlTokenType.Text;
                while (!this.IsAtTagStart && !this.IsAtEndOfStream && !this.IsAtDirectiveStart)
                {
                    if (this.NextCharacter == '<' && !this.IsNextCharacterEntity && _lookAheadCharacter == '?')
                    {
                        // ignore processing directive
                        this.SkipProcessingDirective();
                    }
                    else
                    {
                        if (this.NextCharacter <= ' ')
                        {
                            //  Respect xml:preserve or its equivalents for whitespace processing
                            if (_ignoreNextWhitespace)
                            {
                                // Ignore repeated whitespaces
                            }
                            else
                            {
                                // Treat any control character sequence as one whitespace
                                _nextToken.Append(' ');
                            }
                            _ignoreNextWhitespace = true; // and keep ignoring the following whitespaces
                        }
                        else
                        {
                            _nextToken.Append(this.NextCharacter);
                            _ignoreNextWhitespace = false;
                        }
                        this.GetNextCharacter();
                    }
                }
            }
        }

        /// <summary>
        /// Unconditionally returns a token which is one of: TagEnd, EmptyTagEnd, Name, Atom or EndOfStream
        /// Does not guarantee token reader advancing.
        /// </summary>
        internal void GetNextTagToken()
        {
            _nextToken.Length = 0;
            if (this.IsAtEndOfStream)
            {
                _nextTokenType = HtmlTokenType.EOF;
                return;
            }

            this.SkipWhiteSpace();

            if (this.NextCharacter == '>' && !this.IsNextCharacterEntity)
            {
                // &gt; should not end a tag, so make sure it's not an entity
                _nextTokenType = HtmlTokenType.TagEnd;
                _nextToken.Append('>');
                this.GetNextCharacter();
                // Note: _ignoreNextWhitespace must be set appropriately on tag start processing
            }
            else if (this.NextCharacter == '/' && _lookAheadCharacter == '>')
            {
                // could be start of closing of empty tag
                _nextTokenType = HtmlTokenType.EmptyTagEnd;
                _nextToken.Append("/>");
                this.GetNextCharacter();
                this.GetNextCharacter();
                _ignoreNextWhitespace = false; // Whitespace after no-scope tags are sifnificant
            }
            else if (IsGoodForNameStart(this.NextCharacter))
            {
                _nextTokenType = HtmlTokenType.Name;

                // starts a name
                // we allow character entities here
                // we do not throw exceptions here if end of stream is encountered
                // just stop and return whatever is in the token
                // if the parser is not expecting end of file after this it will call
                // the get next token function and throw an exception
                while (IsGoodForName(this.NextCharacter) && !this.IsAtEndOfStream)
                {
                    _nextToken.Append(this.NextCharacter);
                    this.GetNextCharacter();
                }
            }
            else
            {
                // Unexpected type of token for a tag. Reprot one character as Atom, expecting that HtmlParser will ignore it.
                _nextTokenType = HtmlTokenType.Atom;
                _nextToken.Append(this.NextCharacter);
                this.GetNextCharacter();
            }
        }

        /// <summary>
        /// Unconditionally returns equal sign token. Even if there is no
        /// real equal sign in the stream, it behaves as if it were there.
        /// Does not guarantee token reader advancing.
        /// </summary>
        internal void GetNextEqualSignToken()
        {
            Debug.Assert(_nextTokenType != HtmlTokenType.EOF);
            _nextToken.Length = 0;

            _nextToken.Append('=');
            _nextTokenType = HtmlTokenType.EqualSign;

            this.SkipWhiteSpace();

            if (this.NextCharacter == '=')
            {
                // '=' is not in the list of entities, so no need to check for entities here
                this.GetNextCharacter();
            }
        }

        /// <summary>
        /// Unconditionally returns an atomic value for an attribute
        /// Even if there is no appropriate token it returns Atom value
        /// Does not guarantee token reader advancing.
        /// </summary>
        internal void GetNextAtomToken()
        {
            Debug.Assert(_nextTokenType != HtmlTokenType.EOF);
            _nextToken.Length = 0;

            this.SkipWhiteSpace();

            _nextTokenType = HtmlTokenType.Atom;

            if ((this.NextCharacter == '\'' || this.NextCharacter == '"') && !this.IsNextCharacterEntity)
            {
                char startingQuote = this.NextCharacter;
                this.GetNextCharacter();

                // Consume all characters between quotes
                while (!(this.NextCharacter == startingQuote && !this.IsNextCharacterEntity) && !this.IsAtEndOfStream)
                {
                    _nextToken.Append(this.NextCharacter);
                    this.GetNextCharacter();
                }
                if (this.NextCharacter == startingQuote)
                {
                    this.GetNextCharacter();
                }

                // complete the quoted value
                // NOTE: our recovery here is different from IE's
                // IE keeps reading until it finds a closing quote or end of file
                // if end of file, it treats current value as text
                // if it finds a closing quote at any point within the text, it eats everything between the quotes
                // TODO: Suggestion:
                // however, we could stop when we encounter end of file or an angle bracket of any kind
                // and assume there was a quote there
                // so the attribute value may be meaningless but it is never treated as text
            }
            else
            {
                while (!this.IsAtEndOfStream && !Char.IsWhiteSpace(this.NextCharacter) && this.NextCharacter != '>')
                {
                    _nextToken.Append(this.NextCharacter);
                    this.GetNextCharacter();
                }
            }
        }

        #endregion Internal Methods

        // ---------------------------------------------------------------------
        //
        // Internal Properties
        //
        // ---------------------------------------------------------------------

        #region Internal Properties

        internal HtmlTokenType NextTokenType
        {
            get
            {
                return _nextTokenType;
            }
        }

        internal string NextToken
        {
            get
            {
                return _nextToken.ToString();
            }
        }

        #endregion Internal Properties

        // ---------------------------------------------------------------------
        //
        // Private methods
        //
        // ---------------------------------------------------------------------

        #region Private Methods

        /// <summary>
        /// Advances a reading position by one character code
        /// and reads the next availbale character from a stream.
        /// This character becomes available as NextCharacter property.
        /// </summary>
        /// <remarks>
        /// Throws InvalidOperationException if attempted to be called on EndOfStream
        /// condition.
        /// </remarks>
        private void GetNextCharacter()
        {
            if (_nextCharacterCode == -1)
            {
                throw new InvalidOperationException("GetNextCharacter method called at the end of a stream");
            }

            _previousCharacter = _nextCharacter;

            _nextCharacter = _lookAheadCharacter;
            _nextCharacterCode = _lookAheadCharacterCode;
            // next character not an entity as of now
            _isNextCharacterEntity = false;

            this.ReadLookAheadCharacter();

            if (_nextCharacter == '&')
            {
                if (_lookAheadCharacter == '#')
                {
                    // numeric entity - parse digits - &#DDDDD;
                    int entityCode;
                    entityCode = 0;
                    this.ReadLookAheadCharacter();

                    // largest numeric entity is 7 characters
                    for (int i = 0; i < 7 && Char.IsDigit(_lookAheadCharacter); i++)
                    {
                        entityCode = 10 * entityCode + (_lookAheadCharacterCode - (int)'0');
                        this.ReadLookAheadCharacter();
                    }
                    if (_lookAheadCharacter == ';')
                    {
                        // correct format - advance
                        this.ReadLookAheadCharacter();
                        _nextCharacterCode = entityCode;

                        // if this is out of range it will set the character to '?'
                        _nextCharacter = (char)_nextCharacterCode;

                        // as far as we are concerned, this is an entity
                        _isNextCharacterEntity = true;
                    }
                    else
                    {
                        // not an entity, set next character to the current lookahread character
                        // we would have eaten up some digits
                        _nextCharacter = _lookAheadCharacter;
                        _nextCharacterCode = _lookAheadCharacterCode;
                        this.ReadLookAheadCharacter();
                        _isNextCharacterEntity = false;
                    }
                }
                else if (Char.IsLetter(_lookAheadCharacter))
                {
                    // entity is written as a string
                    string entity = "";

                    // maximum length of string entities is 10 characters
                    for (int i = 0; i < 10 && (Char.IsLetter(_lookAheadCharacter) || Char.IsDigit(_lookAheadCharacter)); i++)
                    {
                        entity += _lookAheadCharacter;
                        this.ReadLookAheadCharacter();
                    }
                    if (_lookAheadCharacter == ';')
                    {
                        // advance
                        this.ReadLookAheadCharacter();

                        if (HtmlSchema.IsEntity(entity))
                        {
                            _nextCharacter = HtmlSchema.EntityCharacterValue(entity);
                            _nextCharacterCode = (int)_nextCharacter;
                            _isNextCharacterEntity = true;
                        }
                        else
                        {
                            // just skip the whole thing - invalid entity
                            // move on to the next character
                            _nextCharacter = _lookAheadCharacter;
                            _nextCharacterCode = _lookAheadCharacterCode;
                            this.ReadLookAheadCharacter();

                            // not an entity
                            _isNextCharacterEntity = false;
                        }
                    }
                    else
                    {
                        // skip whatever we read after the ampersand
                        // set next character and move on
                        _nextCharacter = _lookAheadCharacter;
                        this.ReadLookAheadCharacter();
                        _isNextCharacterEntity = false;
                    }
                }
            }
        }

        private void ReadLookAheadCharacter()
        {
            if (_lookAheadCharacterCode != -1)
            {
                _lookAheadCharacterCode = _inputStringReader.Read();
                _lookAheadCharacter = (char)_lookAheadCharacterCode;
            }
        }

        /// <summary>
        /// skips whitespace in the input string
        /// leaves the first non-whitespace character available in the NextCharacter property
        /// this may be the end-of-file character, it performs no checking
        /// </summary>
        private void SkipWhiteSpace()
        {
            // TODO: handle character entities while processing comments, cdata, and directives
            // TODO: SUGGESTION: we could check if lookahead and previous characters are entities also
            while (true)
            {
                if (_nextCharacter == '<' && (_lookAheadCharacter == '?' || _lookAheadCharacter == '!'))
                {
                    this.GetNextCharacter();

                    if (_lookAheadCharacter == '[')
                    {
                        // Skip CDATA block and DTDs(?)
                        while (!this.IsAtEndOfStream && !(_previousCharacter == ']' && _nextCharacter == ']' && _lookAheadCharacter == '>'))
                        {
                            this.GetNextCharacter();
                        }
                        if (_nextCharacter == '>')
                        {
                            this.GetNextCharacter();
                        }
                    }
                    else
                    {
                        // Skip processing instruction, comments
                        while (!this.IsAtEndOfStream && _nextCharacter != '>')
                        {
                            this.GetNextCharacter();
                        }
                        if (_nextCharacter == '>')
                        {
                            this.GetNextCharacter();
                        }
                    }
                }


                if (!Char.IsWhiteSpace(this.NextCharacter))
                {
                    break;
                }

                this.GetNextCharacter();
            }
        }

        /// <summary>
        /// checks if a character can be used to start a name
        /// if this check is true then the rest of the name can be read
        /// </summary>
        /// <param name="character">
        /// character value to be checked
        /// </param>
        /// <returns>
        /// true if the character can be the first character in a name
        /// false otherwise
        /// </returns>
        private bool IsGoodForNameStart(char character)
        {
            return character == '_' || Char.IsLetter(character);
        }

        /// <summary>
        /// checks if a character can be used as a non-starting character in a name
        /// uses the IsExtender and IsCombiningCharacter predicates to see
        /// if a character is an extender or a combining character
        /// </summary>
        /// <param name="character">
        /// character to be checked for validity in a name
        /// </param>
        /// <returns>
        /// true if the character can be a valid part of a name
        /// </returns>
        private bool IsGoodForName(char character)
        {
            // we are not concerned with escaped characters in names
            // we assume that character entities are allowed as part of a name
            return
                this.IsGoodForNameStart(character) ||
                character == '.' ||
                character == '-' ||
                character == ':' ||
                Char.IsDigit(character) ||
                IsCombiningCharacter(character) ||
                IsExtender(character);
        }

        /// <summary>
        /// identifies a character as being a combining character, permitted in a name
        /// TODO: only a placeholder for now but later to be replaced with comparisons against
        /// the list of combining characters in the XML documentation
        /// </summary>
        /// <param name="character">
        /// character to be checked
        /// </param>
        /// <returns>
        /// true if the character is a combining character, false otherwise
        /// </returns>
        private bool IsCombiningCharacter(char character)
        {
            // TODO: put actual code with checks against all combining characters here
            return false;
        }

        /// <summary>
        /// identifies a character as being an extender, permitted in a name
        /// TODO: only a placeholder for now but later to be replaced with comparisons against
        /// the list of extenders in the XML documentation
        /// </summary>
        /// <param name="character">
        /// character to be checked
        /// </param>
        /// <returns>
        /// true if the character is an extender, false otherwise
        /// </returns>
        private bool IsExtender(char character)
        {
            // TODO: put actual code with checks against all extenders here
            return false;
        }

        /// <summary>
        /// skips dynamic content starting with '<![' and ending with ']>'
        /// </summary>
        private void ReadDynamicContent()
        {
            // verify that we are at dynamic content, which may include CDATA
            Debug.Assert(_previousCharacter == '<' && _nextCharacter == '!' && _lookAheadCharacter == '[');

            // Let's treat this as empty text
            _nextTokenType = HtmlTokenType.Text;
            _nextToken.Length = 0;

            // advance twice, once to get the lookahead character and then to reach the start of the cdata
            this.GetNextCharacter();
            this.GetNextCharacter();

            // NOTE: 10/12/2004: modified this function to check when called if's reading CDATA or something else
            // some directives may start with a <![ and then have some data and they will just end with a ]>
            // this function is modified to stop at the sequence ]> and not ]]>
            // this means that CDATA and anything else expressed in their own set of [] within the <! [...]>
            // directive cannot contain a ]> sequence. However it is doubtful that cdata could contain such
            // sequence anyway, it probably stops at the first ]
            while (!(_nextCharacter == ']' && _lookAheadCharacter == '>') && !this.IsAtEndOfStream)
            {
                // advance
                this.GetNextCharacter();
            }

            if (!this.IsAtEndOfStream)
            {
                // advance, first to the last >
                this.GetNextCharacter();

                // then advance past it to the next character after processing directive
                this.GetNextCharacter();
            }
        }

        /// <summary>
        /// skips comments starting with '<!-' and ending with '-->'
        /// NOTE: 10/06/2004: processing changed, will now skip anything starting with
        /// the "<!-"  sequence and ending in "!>" or "->", because in practice many html pages do not
        /// use the full comment specifying conventions
        /// </summary>
        private void ReadComment()
        {
            // verify that we are at a comment
            Debug.Assert(_previousCharacter == '<' && _nextCharacter == '!' && _lookAheadCharacter == '-');

            // Initialize a token
            _nextTokenType = HtmlTokenType.Comment;
            _nextToken.Length = 0;

            // advance to the next character, so that to be at the start of comment value
            this.GetNextCharacter(); // get first '-'
            this.GetNextCharacter(); // get second '-'
            this.GetNextCharacter(); // get first character of comment content

            while (true)
            {
                // Read text until end of comment
                // Note that in many actual html pages comments end with "!>" (while xml standard is "-->")
                while (!this.IsAtEndOfStream && !(_nextCharacter == '-' && _lookAheadCharacter == '-' || _nextCharacter == '!' && _lookAheadCharacter == '>'))
                {
                    _nextToken.Append(this.NextCharacter);
                    this.GetNextCharacter();
                }

                // Finish comment reading
                this.GetNextCharacter();
                if (_previousCharacter == '-' && _nextCharacter == '-' && _lookAheadCharacter == '>')
                {
                    // Standard comment end. Eat it and exit the loop
                    this.GetNextCharacter(); // get '>'
                    break;
                }
                else if (_previousCharacter == '!' && _nextCharacter == '>')
                {
                    // Nonstandard but possible comment end - '!>'. Exit the loop
                    break;
                }
                else
                {
                    // Not an end. Save character and continue continue reading
                    _nextToken.Append(_previousCharacter);
                    continue;
                }
            }

            // Read end of comment combination
            if (_nextCharacter == '>')
            {
                this.GetNextCharacter();
            }
        }

        /// <summary>
        /// skips past unknown directives that start with "<!" but are not comments or Cdata
        /// ignores content of such directives until the next ">" character
        /// applies to directives such as DOCTYPE, etc that we do not presently support
        /// </summary>
        private void ReadUnknownDirective()
        {
            // verify that we are at an unknown directive
            Debug.Assert(_previousCharacter == '<' && _nextCharacter == '!' && !(_lookAheadCharacter == '-' || _lookAheadCharacter == '['));

            // Let's treat this as empty text
            _nextTokenType = HtmlTokenType.Text;
            _nextToken.Length = 0;

            // advance to the next character
            this.GetNextCharacter();

            // skip to the first tag end we find
            while (!(_nextCharacter == '>' && !IsNextCharacterEntity) && !this.IsAtEndOfStream)
            {
                this.GetNextCharacter();
            }

            if (!this.IsAtEndOfStream)
            {
                // advance past the tag end
                this.GetNextCharacter();
            }
        }

        /// <summary>
        /// skips processing directives starting with the characters '<?' and ending with '?>'
        /// NOTE: 10/14/2004: IE also ends processing directives with a />, so this function is
        /// being modified to recognize that condition as well
        /// </summary>
        private void SkipProcessingDirective()
        {
            // verify that we are at a processing directive
            Debug.Assert(_nextCharacter == '<' && _lookAheadCharacter == '?');

            // advance twice, once to get the lookahead character and then to reach the start of the drective
            this.GetNextCharacter();
            this.GetNextCharacter();

            while (!((_nextCharacter == '?' || _nextCharacter == '/') && _lookAheadCharacter == '>') && !this.IsAtEndOfStream)
            {
                // advance
                // we don't need to check for entities here because '?' is not an entity
                // and even though > is an entity there is no entity processing when reading lookahead character
                this.GetNextCharacter();
            }

            if (!this.IsAtEndOfStream)
            {
                // advance, first to the last >
                this.GetNextCharacter();

                // then advance past it to the next character after processing directive
                this.GetNextCharacter();
            }
        }

        #endregion Private Methods

        // ---------------------------------------------------------------------
        //
        // Private Properties
        //
        // ---------------------------------------------------------------------

        #region Private Properties

        private char NextCharacter
        {
            get
            {
                return _nextCharacter;
            }
        }

        private bool IsAtEndOfStream
        {
            get
            {
                return _nextCharacterCode == -1;
            }
        }

        private bool IsAtTagStart
        {
            get
            {
                return _nextCharacter == '<' && (_lookAheadCharacter == '/' || IsGoodForNameStart(_lookAheadCharacter)) && !_isNextCharacterEntity;
            }
        }

        private bool IsAtTagEnd
        {
            // check if at end of empty tag or regular tag
            get
            {
                return (_nextCharacter == '>' || (_nextCharacter == '/' && _lookAheadCharacter == '>')) && !_isNextCharacterEntity;
            }
        }

        private bool IsAtDirectiveStart
        {
            get
            {
                return (_nextCharacter == '<' && _lookAheadCharacter == '!' && !this.IsNextCharacterEntity);
            }
        }

        private bool IsNextCharacterEntity
        {
            // check if next character is an entity
            get
            {
                return _isNextCharacterEntity;
            }
        }

        #endregion Private Properties

        // ---------------------------------------------------------------------
        //
        // Private Fields
        //
        // ---------------------------------------------------------------------

        #region Private Fields

        // string reader which will move over input text
        private StringReader _inputStringReader;
        // next character code read from input that is not yet part of any token
        // and the character it represents
        private int _nextCharacterCode;
        private char _nextCharacter;
        private int _lookAheadCharacterCode;
        private char _lookAheadCharacter;
        private char _previousCharacter;
        private bool _ignoreNextWhitespace;
        private bool _isNextCharacterEntity;

        // store token and type in local variables before copying them to output parameters
        StringBuilder _nextToken;
        HtmlTokenType _nextTokenType;

        #endregion Private Fields
    }

    internal class HtmlSchema
    {
        #region Constructors
        static HtmlSchema()
        {
            InitializeInlineElements();
            InitializeBlockElements();
            InitializeOtherOpenableElements();
            InitializeEmptyElements();
            InitializeElementsClosingOnParentElementEnd();
            InitializeElementsClosingOnNewElementStart();
            InitializeHtmlCharacterEntities();
        }

        #endregion Constructors;

        #region Internal Methods

        /// <summary>
        /// returns true when xmlElementName corresponds to empty element
        /// </summary>
        /// <param name="xmlElementName">
        /// string representing name to test
        /// </param>
        internal static bool IsEmptyElement(string xmlElementName)
        {
            // convert to lowercase before we check
            // because element names are not case sensitive
            return _htmlEmptyElements.Contains(xmlElementName.ToLower());
        }

        /// <summary>
        /// returns true if xmlElementName represents a block formattinng element.
        /// It used in an algorithm of transferring inline elements over block elements
        /// in HtmlParser
        /// </summary>
        /// <param name="xmlElementName"></param>
        /// <returns></returns>
        internal static bool IsBlockElement(string xmlElementName)
        {
            return _htmlBlockElements.Contains(xmlElementName);
        }

        /// <summary>
        /// returns true if the xmlElementName represents an inline formatting element
        /// </summary>
        /// <param name="xmlElementName"></param>
        /// <returns></returns>
        internal static bool IsInlineElement(string xmlElementName)
        {
            return _htmlInlineElements.Contains(xmlElementName);
        }

        /// <summary>
        /// It is a list of known html elements which we
        /// want to allow to produce bt HTML parser,
        /// but don'tt want to act as inline, block or no-scope.
        /// Presence in this list will allow to open
        /// elements during html parsing, and adding the
        /// to a tree produced by html parser.
        /// </summary>
        internal static bool IsKnownOpenableElement(string xmlElementName)
        {
            return _htmlOtherOpenableElements.Contains(xmlElementName);
        }

        /// <summary>
        /// returns true when xmlElementName closes when the outer element closes
        /// this is true of elements with optional start tags
        /// </summary>
        /// <param name="xmlElementName">
        /// string representing name to test
        /// </param>
        internal static bool ClosesOnParentElementEnd(string xmlElementName)
        {
            // convert to lowercase when testing
            return _htmlElementsClosingOnParentElementEnd.Contains(xmlElementName.ToLower());
        }

        /// <summary>
        /// returns true if the current element closes when the new element, whose name has just been read, starts
        /// </summary>
        /// <param name="currentElementName">
        /// string representing current element name
        /// </param>
        /// <param name="elementName"></param>
        /// string representing name of the next element that will start
        internal static bool ClosesOnNextElementStart(string currentElementName, string nextElementName)
        {
            Debug.Assert(currentElementName == currentElementName.ToLower());
            switch (currentElementName)
            {
                case "colgroup":
                    return _htmlElementsClosingColgroup.Contains(nextElementName) && HtmlSchema.IsBlockElement(nextElementName);
                case "dd":
                    return _htmlElementsClosingDD.Contains(nextElementName) && HtmlSchema.IsBlockElement(nextElementName);
                case "dt":
                    return _htmlElementsClosingDT.Contains(nextElementName) && HtmlSchema.IsBlockElement(nextElementName);
                case "li":
                    return _htmlElementsClosingLI.Contains(nextElementName);
                case "p":
                    return HtmlSchema.IsBlockElement(nextElementName);
                case "tbody":
                    return _htmlElementsClosingTbody.Contains(nextElementName);
                case "tfoot":
                    return _htmlElementsClosingTfoot.Contains(nextElementName);
                case "thead":
                    return _htmlElementsClosingThead.Contains(nextElementName);
                case "tr":
                    return _htmlElementsClosingTR.Contains(nextElementName);
                case "td":
                    return _htmlElementsClosingTD.Contains(nextElementName);
                case "th":
                    return _htmlElementsClosingTH.Contains(nextElementName);
            }
            return false;
        }

        /// <summary>
        /// returns true if the string passed as argument is an Html entity name
        /// </summary>
        /// <param name="entityName">
        /// string to be tested for Html entity name
        /// </param>
        internal static bool IsEntity(string entityName)
        {
            // we do not convert entity strings to lowercase because these names are case-sensitive
            if (_htmlCharacterEntities.ContainsKey(entityName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// returns the character represented by the entity name string which is passed as an argument, if the string is an entity name
        /// as specified in _htmlCharacterEntities, returns the character value of 0 otherwise
        /// </summary>
        /// <param name="entityName">
        /// string representing entity name whose character value is desired
        /// </param>
        internal static char EntityCharacterValue(string entityName)
        {
            if (_htmlCharacterEntities.ContainsKey(entityName))
            {
                return (char)_htmlCharacterEntities[entityName];
            }
            else
            {
                return (char)0;
            }
        }

        #endregion Internal Methods

        #region Internal Properties

        #endregion Internal Indexers

        #region Private Methods

        private static void InitializeInlineElements()
        {
            _htmlInlineElements = new List<object>();
            _htmlInlineElements.Add("a");
            _htmlInlineElements.Add("abbr");
            _htmlInlineElements.Add("acronym");
            _htmlInlineElements.Add("address");
            _htmlInlineElements.Add("b");
            _htmlInlineElements.Add("bdo"); // ???
            _htmlInlineElements.Add("big");
            _htmlInlineElements.Add("button");
            _htmlInlineElements.Add("code");
            _htmlInlineElements.Add("del"); // deleted text
            _htmlInlineElements.Add("dfn");
            _htmlInlineElements.Add("em");
            _htmlInlineElements.Add("font");
            _htmlInlineElements.Add("i");
            _htmlInlineElements.Add("idea");
            _htmlInlineElements.Add("ins"); // inserted text
            _htmlInlineElements.Add("kbd"); // text to entered by a user
            _htmlInlineElements.Add("label");
            _htmlInlineElements.Add("legend"); // ???
            _htmlInlineElements.Add("q"); // short inline quotation
            _htmlInlineElements.Add("s"); // strike-through text style
            _htmlInlineElements.Add("samp"); // Specifies a code sample
            _htmlInlineElements.Add("small");
            _htmlInlineElements.Add("span");
            _htmlInlineElements.Add("strike");
            _htmlInlineElements.Add("strong");
            _htmlInlineElements.Add("sub");
            _htmlInlineElements.Add("sup");
            _htmlInlineElements.Add("u");
            _htmlInlineElements.Add("var"); // indicates an instance of a program variable
        }

        private static void InitializeBlockElements()
        {
            _htmlBlockElements = new List<object>();

            _htmlBlockElements.Add("blockquote");
            _htmlBlockElements.Add("body");
            _htmlBlockElements.Add("caption");
            _htmlBlockElements.Add("center");
            _htmlBlockElements.Add("cite");
            _htmlBlockElements.Add("dd");
            _htmlBlockElements.Add("dir"); //  treat as UL element
            _htmlBlockElements.Add("div");
            _htmlBlockElements.Add("dl");
            _htmlBlockElements.Add("dt");
            _htmlBlockElements.Add("form"); // Not a block according to XHTML spec
            _htmlBlockElements.Add("h1");
            _htmlBlockElements.Add("h2");
            _htmlBlockElements.Add("h3");
            _htmlBlockElements.Add("h4");
            _htmlBlockElements.Add("h5");
            _htmlBlockElements.Add("h6");
            _htmlBlockElements.Add("html");
            _htmlBlockElements.Add("li");
            _htmlBlockElements.Add("menu"); //  treat as UL element
            _htmlBlockElements.Add("ol");
            _htmlBlockElements.Add("p");
            _htmlBlockElements.Add("pre"); // Renders text in a fixed-width font
            _htmlBlockElements.Add("table");
            _htmlBlockElements.Add("tbody");
            _htmlBlockElements.Add("td");
            _htmlBlockElements.Add("textarea");
            _htmlBlockElements.Add("tfoot");
            _htmlBlockElements.Add("th");
            _htmlBlockElements.Add("thead");
            _htmlBlockElements.Add("tr");
            _htmlBlockElements.Add("tt");
            _htmlBlockElements.Add("ul");
        }

        /// <summary>
        /// initializes _htmlEmptyElements with empty elements in HTML 4 spec at
        /// http://www.w3.org/TR/REC-html40/index/elements.html
        /// </summary>
        private static void InitializeEmptyElements()
        {
            // Build a list of empty (no-scope) elements
            // (element not requiring closing tags, and not accepting any content)
            _htmlEmptyElements = new List<object>();
            _htmlEmptyElements.Add("area");
            _htmlEmptyElements.Add("base");
            _htmlEmptyElements.Add("basefont");
            _htmlEmptyElements.Add("br");
            _htmlEmptyElements.Add("col");
            _htmlEmptyElements.Add("frame");
            _htmlEmptyElements.Add("hr");
            _htmlEmptyElements.Add("img");
            _htmlEmptyElements.Add("input");
            _htmlEmptyElements.Add("isindex");
            _htmlEmptyElements.Add("link");
            _htmlEmptyElements.Add("meta");
            _htmlEmptyElements.Add("param");
        }

        private static void InitializeOtherOpenableElements()
        {
            // It is a list of known html elements which we
            // want to allow to produce bt HTML parser,
            // but don'tt want to act as inline, block or no-scope.
            // Presence in this list will allow to open
            // elements during html parsing, and adding the
            // to a tree produced by html parser.
            _htmlOtherOpenableElements = new List<object>();
            _htmlOtherOpenableElements.Add("applet");
            _htmlOtherOpenableElements.Add("base");
            _htmlOtherOpenableElements.Add("basefont");
            _htmlOtherOpenableElements.Add("colgroup");
            _htmlOtherOpenableElements.Add("fieldset");
            //_htmlOtherOpenableElements.Add("form"); --> treated as block
            _htmlOtherOpenableElements.Add("frameset");
            _htmlOtherOpenableElements.Add("head");
            _htmlOtherOpenableElements.Add("iframe");
            _htmlOtherOpenableElements.Add("map");
            _htmlOtherOpenableElements.Add("noframes");
            _htmlOtherOpenableElements.Add("noscript");
            _htmlOtherOpenableElements.Add("object");
            _htmlOtherOpenableElements.Add("optgroup");
            _htmlOtherOpenableElements.Add("option");
            _htmlOtherOpenableElements.Add("script");
            _htmlOtherOpenableElements.Add("select");
            _htmlOtherOpenableElements.Add("style");
            _htmlOtherOpenableElements.Add("title");
        }

        /// <summary>
        /// initializes _htmlElementsClosingOnParentElementEnd with the list of HTML 4 elements for which closing tags are optional
        /// we assume that for any element for which closing tags are optional, the element closes when it's outer element
        /// (in which it is nested) does
        /// </summary>
        private static void InitializeElementsClosingOnParentElementEnd()
        {
            _htmlElementsClosingOnParentElementEnd = new List<object>();
            _htmlElementsClosingOnParentElementEnd.Add("body");
            _htmlElementsClosingOnParentElementEnd.Add("colgroup");
            _htmlElementsClosingOnParentElementEnd.Add("dd");
            _htmlElementsClosingOnParentElementEnd.Add("dt");
            _htmlElementsClosingOnParentElementEnd.Add("head");
            _htmlElementsClosingOnParentElementEnd.Add("html");
            _htmlElementsClosingOnParentElementEnd.Add("li");
            _htmlElementsClosingOnParentElementEnd.Add("p");
            _htmlElementsClosingOnParentElementEnd.Add("tbody");
            _htmlElementsClosingOnParentElementEnd.Add("td");
            _htmlElementsClosingOnParentElementEnd.Add("tfoot");
            _htmlElementsClosingOnParentElementEnd.Add("thead");
            _htmlElementsClosingOnParentElementEnd.Add("th");
            _htmlElementsClosingOnParentElementEnd.Add("tr");
        }

        private static void InitializeElementsClosingOnNewElementStart()
        {
            _htmlElementsClosingColgroup = new List<object>();
            _htmlElementsClosingColgroup.Add("colgroup");
            _htmlElementsClosingColgroup.Add("tr");
            _htmlElementsClosingColgroup.Add("thead");
            _htmlElementsClosingColgroup.Add("tfoot");
            _htmlElementsClosingColgroup.Add("tbody");

            _htmlElementsClosingDD = new List<object>();
            _htmlElementsClosingDD.Add("dd");
            _htmlElementsClosingDD.Add("dt");
            // TODO: dd may end in other cases as well - if a new "p" starts, etc.
            // TODO: these are the basic "legal" cases but there may be more recovery

            _htmlElementsClosingDT = new List<object>();
            _htmlElementsClosingDD.Add("dd");
            _htmlElementsClosingDD.Add("dt");
            // TODO: dd may end in other cases as well - if a new "p" starts, etc.
            // TODO: these are the basic "legal" cases but there may be more recovery

            _htmlElementsClosingLI = new List<object>();
            _htmlElementsClosingLI.Add("li");
            // TODO: more complex recovery

            _htmlElementsClosingTbody = new List<object>();
            _htmlElementsClosingTbody.Add("tbody");
            _htmlElementsClosingTbody.Add("thead");
            _htmlElementsClosingTbody.Add("tfoot");
            // TODO: more complex recovery

            _htmlElementsClosingTR = new List<object>();
            // NOTE: tr should not really close on a new thead
            // because if there are rows before a thead, it is assumed to be in tbody, whose start tag is optional
            // and thead can't come after tbody
            // however, if we do encounter this, it's probably best to end the row and ignore the thead or treat
            // it as part of the table
            _htmlElementsClosingTR.Add("thead");
            _htmlElementsClosingTR.Add("tfoot");
            _htmlElementsClosingTR.Add("tbody");
            _htmlElementsClosingTR.Add("tr");
            // TODO: more complex recovery

            _htmlElementsClosingTD = new List<object>();
            _htmlElementsClosingTD.Add("td");
            _htmlElementsClosingTD.Add("th");
            _htmlElementsClosingTD.Add("tr");
            _htmlElementsClosingTD.Add("tbody");
            _htmlElementsClosingTD.Add("tfoot");
            _htmlElementsClosingTD.Add("thead");
            // TODO: more complex recovery

            _htmlElementsClosingTH = new List<object>();
            _htmlElementsClosingTH.Add("td");
            _htmlElementsClosingTH.Add("th");
            _htmlElementsClosingTH.Add("tr");
            _htmlElementsClosingTH.Add("tbody");
            _htmlElementsClosingTH.Add("tfoot");
            _htmlElementsClosingTH.Add("thead");
            // TODO: more complex recovery

            _htmlElementsClosingThead = new List<object>();
            _htmlElementsClosingThead.Add("tbody");
            _htmlElementsClosingThead.Add("tfoot");
            // TODO: more complex recovery

            _htmlElementsClosingTfoot = new List<object>();
            _htmlElementsClosingTfoot.Add("tbody");
            // although thead comes before tfoot, we add it because if it is found the tfoot should close
            // and some recovery processing be done on the thead
            _htmlElementsClosingTfoot.Add("thead");
            // TODO: more complex recovery
        }

        /// <summary>
        /// initializes _htmlCharacterEntities hashtable with the character corresponding to entity names
        /// </summary>
        private static void InitializeHtmlCharacterEntities()
        {
            _htmlCharacterEntities = new Dictionary<object, object>();
            _htmlCharacterEntities["Aacute"] = (char)193;
            _htmlCharacterEntities["aacute"] = (char)225;
            _htmlCharacterEntities["Acirc"] = (char)194;
            _htmlCharacterEntities["acirc"] = (char)226;
            _htmlCharacterEntities["acute"] = (char)180;
            _htmlCharacterEntities["AElig"] = (char)198;
            _htmlCharacterEntities["aelig"] = (char)230;
            _htmlCharacterEntities["Agrave"] = (char)192;
            _htmlCharacterEntities["agrave"] = (char)224;
            _htmlCharacterEntities["alefsym"] = (char)8501;
            _htmlCharacterEntities["Alpha"] = (char)913;
            _htmlCharacterEntities["alpha"] = (char)945;
            _htmlCharacterEntities["amp"] = (char)38;
            _htmlCharacterEntities["and"] = (char)8743;
            _htmlCharacterEntities["ang"] = (char)8736;
            _htmlCharacterEntities["Aring"] = (char)197;
            _htmlCharacterEntities["aring"] = (char)229;
            _htmlCharacterEntities["asymp"] = (char)8776;
            _htmlCharacterEntities["Atilde"] = (char)195;
            _htmlCharacterEntities["atilde"] = (char)227;
            _htmlCharacterEntities["Auml"] = (char)196;
            _htmlCharacterEntities["auml"] = (char)228;
            _htmlCharacterEntities["bdquo"] = (char)8222;
            _htmlCharacterEntities["Beta"] = (char)914;
            _htmlCharacterEntities["beta"] = (char)946;
            _htmlCharacterEntities["brvbar"] = (char)166;
            _htmlCharacterEntities["bull"] = (char)8226;
            _htmlCharacterEntities["cap"] = (char)8745;
            _htmlCharacterEntities["Ccedil"] = (char)199;
            _htmlCharacterEntities["ccedil"] = (char)231;
            _htmlCharacterEntities["cent"] = (char)162;
            _htmlCharacterEntities["Chi"] = (char)935;
            _htmlCharacterEntities["chi"] = (char)967;
            _htmlCharacterEntities["circ"] = (char)710;
            _htmlCharacterEntities["clubs"] = (char)9827;
            _htmlCharacterEntities["cong"] = (char)8773;
            _htmlCharacterEntities["copy"] = (char)169;
            _htmlCharacterEntities["crarr"] = (char)8629;
            _htmlCharacterEntities["cup"] = (char)8746;
            _htmlCharacterEntities["curren"] = (char)164;
            _htmlCharacterEntities["dagger"] = (char)8224;
            _htmlCharacterEntities["Dagger"] = (char)8225;
            _htmlCharacterEntities["darr"] = (char)8595;
            _htmlCharacterEntities["dArr"] = (char)8659;
            _htmlCharacterEntities["deg"] = (char)176;
            _htmlCharacterEntities["Delta"] = (char)916;
            _htmlCharacterEntities["delta"] = (char)948;
            _htmlCharacterEntities["diams"] = (char)9830;
            _htmlCharacterEntities["divide"] = (char)247;
            _htmlCharacterEntities["Eacute"] = (char)201;
            _htmlCharacterEntities["eacute"] = (char)233;
            _htmlCharacterEntities["Ecirc"] = (char)202;
            _htmlCharacterEntities["ecirc"] = (char)234;
            _htmlCharacterEntities["Egrave"] = (char)200;
            _htmlCharacterEntities["egrave"] = (char)232;
            _htmlCharacterEntities["empty"] = (char)8709;
            _htmlCharacterEntities["emsp"] = (char)8195;
            _htmlCharacterEntities["ensp"] = (char)8194;
            _htmlCharacterEntities["Epsilon"] = (char)917;
            _htmlCharacterEntities["epsilon"] = (char)949;
            _htmlCharacterEntities["equiv"] = (char)8801;
            _htmlCharacterEntities["Eta"] = (char)919;
            _htmlCharacterEntities["eta"] = (char)951;
            _htmlCharacterEntities["ETH"] = (char)208;
            _htmlCharacterEntities["eth"] = (char)240;
            _htmlCharacterEntities["Euml"] = (char)203;
            _htmlCharacterEntities["euml"] = (char)235;
            _htmlCharacterEntities["euro"] = (char)8364;
            _htmlCharacterEntities["exist"] = (char)8707;
            _htmlCharacterEntities["fnof"] = (char)402;
            _htmlCharacterEntities["forall"] = (char)8704;
            _htmlCharacterEntities["frac12"] = (char)189;
            _htmlCharacterEntities["frac14"] = (char)188;
            _htmlCharacterEntities["frac34"] = (char)190;
            _htmlCharacterEntities["frasl"] = (char)8260;
            _htmlCharacterEntities["Gamma"] = (char)915;
            _htmlCharacterEntities["gamma"] = (char)947;
            _htmlCharacterEntities["ge"] = (char)8805;
            _htmlCharacterEntities["gt"] = (char)62;
            _htmlCharacterEntities["harr"] = (char)8596;
            _htmlCharacterEntities["hArr"] = (char)8660;
            _htmlCharacterEntities["hearts"] = (char)9829;
            _htmlCharacterEntities["hellip"] = (char)8230;
            _htmlCharacterEntities["Iacute"] = (char)205;
            _htmlCharacterEntities["iacute"] = (char)237;
            _htmlCharacterEntities["Icirc"] = (char)206;
            _htmlCharacterEntities["icirc"] = (char)238;
            _htmlCharacterEntities["iexcl"] = (char)161;
            _htmlCharacterEntities["Igrave"] = (char)204;
            _htmlCharacterEntities["igrave"] = (char)236;
            _htmlCharacterEntities["image"] = (char)8465;
            _htmlCharacterEntities["infin"] = (char)8734;
            _htmlCharacterEntities["int"] = (char)8747;
            _htmlCharacterEntities["Iota"] = (char)921;
            _htmlCharacterEntities["iota"] = (char)953;
            _htmlCharacterEntities["iquest"] = (char)191;
            _htmlCharacterEntities["isin"] = (char)8712;
            _htmlCharacterEntities["Iuml"] = (char)207;
            _htmlCharacterEntities["iuml"] = (char)239;
            _htmlCharacterEntities["Kappa"] = (char)922;
            _htmlCharacterEntities["kappa"] = (char)954;
            _htmlCharacterEntities["Lambda"] = (char)923;
            _htmlCharacterEntities["lambda"] = (char)955;
            _htmlCharacterEntities["lang"] = (char)9001;
            _htmlCharacterEntities["laquo"] = (char)171;
            _htmlCharacterEntities["larr"] = (char)8592;
            _htmlCharacterEntities["lArr"] = (char)8656;
            _htmlCharacterEntities["lceil"] = (char)8968;
            _htmlCharacterEntities["ldquo"] = (char)8220;
            _htmlCharacterEntities["le"] = (char)8804;
            _htmlCharacterEntities["lfloor"] = (char)8970;
            _htmlCharacterEntities["lowast"] = (char)8727;
            _htmlCharacterEntities["loz"] = (char)9674;
            _htmlCharacterEntities["lrm"] = (char)8206;
            _htmlCharacterEntities["lsaquo"] = (char)8249;
            _htmlCharacterEntities["lsquo"] = (char)8216;
            _htmlCharacterEntities["lt"] = (char)60;
            _htmlCharacterEntities["macr"] = (char)175;
            _htmlCharacterEntities["mdash"] = (char)8212;
            _htmlCharacterEntities["micro"] = (char)181;
            _htmlCharacterEntities["middot"] = (char)183;
            _htmlCharacterEntities["minus"] = (char)8722;
            _htmlCharacterEntities["Mu"] = (char)924;
            _htmlCharacterEntities["mu"] = (char)956;
            _htmlCharacterEntities["nabla"] = (char)8711;
            _htmlCharacterEntities["nbsp"] = (char)160;
            _htmlCharacterEntities["ndash"] = (char)8211;
            _htmlCharacterEntities["ne"] = (char)8800;
            _htmlCharacterEntities["ni"] = (char)8715;
            _htmlCharacterEntities["not"] = (char)172;
            _htmlCharacterEntities["notin"] = (char)8713;
            _htmlCharacterEntities["nsub"] = (char)8836;
            _htmlCharacterEntities["Ntilde"] = (char)209;
            _htmlCharacterEntities["ntilde"] = (char)241;
            _htmlCharacterEntities["Nu"] = (char)925;
            _htmlCharacterEntities["nu"] = (char)957;
            _htmlCharacterEntities["Oacute"] = (char)211;
            _htmlCharacterEntities["ocirc"] = (char)244;
            _htmlCharacterEntities["OElig"] = (char)338;
            _htmlCharacterEntities["oelig"] = (char)339;
            _htmlCharacterEntities["Ograve"] = (char)210;
            _htmlCharacterEntities["ograve"] = (char)242;
            _htmlCharacterEntities["oline"] = (char)8254;
            _htmlCharacterEntities["Omega"] = (char)937;
            _htmlCharacterEntities["omega"] = (char)969;
            _htmlCharacterEntities["Omicron"] = (char)927;
            _htmlCharacterEntities["omicron"] = (char)959;
            _htmlCharacterEntities["oplus"] = (char)8853;
            _htmlCharacterEntities["or"] = (char)8744;
            _htmlCharacterEntities["ordf"] = (char)170;
            _htmlCharacterEntities["ordm"] = (char)186;
            _htmlCharacterEntities["Oslash"] = (char)216;
            _htmlCharacterEntities["oslash"] = (char)248;
            _htmlCharacterEntities["Otilde"] = (char)213;
            _htmlCharacterEntities["otilde"] = (char)245;
            _htmlCharacterEntities["otimes"] = (char)8855;
            _htmlCharacterEntities["Ouml"] = (char)214;
            _htmlCharacterEntities["ouml"] = (char)246;
            _htmlCharacterEntities["para"] = (char)182;
            _htmlCharacterEntities["part"] = (char)8706;
            _htmlCharacterEntities["permil"] = (char)8240;
            _htmlCharacterEntities["perp"] = (char)8869;
            _htmlCharacterEntities["Phi"] = (char)934;
            _htmlCharacterEntities["phi"] = (char)966;
            _htmlCharacterEntities["pi"] = (char)960;
            _htmlCharacterEntities["piv"] = (char)982;
            _htmlCharacterEntities["plusmn"] = (char)177;
            _htmlCharacterEntities["pound"] = (char)163;
            _htmlCharacterEntities["prime"] = (char)8242;
            _htmlCharacterEntities["Prime"] = (char)8243;
            _htmlCharacterEntities["prod"] = (char)8719;
            _htmlCharacterEntities["prop"] = (char)8733;
            _htmlCharacterEntities["Psi"] = (char)936;
            _htmlCharacterEntities["psi"] = (char)968;
            _htmlCharacterEntities["quot"] = (char)34;
            _htmlCharacterEntities["radic"] = (char)8730;
            _htmlCharacterEntities["rang"] = (char)9002;
            _htmlCharacterEntities["raquo"] = (char)187;
            _htmlCharacterEntities["rarr"] = (char)8594;
            _htmlCharacterEntities["rArr"] = (char)8658;
            _htmlCharacterEntities["rceil"] = (char)8969;
            _htmlCharacterEntities["rdquo"] = (char)8221;
            _htmlCharacterEntities["real"] = (char)8476;
            _htmlCharacterEntities["reg"] = (char)174;
            _htmlCharacterEntities["rfloor"] = (char)8971;
            _htmlCharacterEntities["Rho"] = (char)929;
            _htmlCharacterEntities["rho"] = (char)961;
            _htmlCharacterEntities["rlm"] = (char)8207;
            _htmlCharacterEntities["rsaquo"] = (char)8250;
            _htmlCharacterEntities["rsquo"] = (char)8217;
            _htmlCharacterEntities["sbquo"] = (char)8218;
            _htmlCharacterEntities["Scaron"] = (char)352;
            _htmlCharacterEntities["scaron"] = (char)353;
            _htmlCharacterEntities["sdot"] = (char)8901;
            _htmlCharacterEntities["sect"] = (char)167;
            _htmlCharacterEntities["shy"] = (char)173;
            _htmlCharacterEntities["Sigma"] = (char)931;
            _htmlCharacterEntities["sigma"] = (char)963;
            _htmlCharacterEntities["sigmaf"] = (char)962;
            _htmlCharacterEntities["sim"] = (char)8764;
            _htmlCharacterEntities["spades"] = (char)9824;
            _htmlCharacterEntities["sub"] = (char)8834;
            _htmlCharacterEntities["sube"] = (char)8838;
            _htmlCharacterEntities["sum"] = (char)8721;
            _htmlCharacterEntities["sup"] = (char)8835;
            _htmlCharacterEntities["sup1"] = (char)185;
            _htmlCharacterEntities["sup2"] = (char)178;
            _htmlCharacterEntities["sup3"] = (char)179;
            _htmlCharacterEntities["supe"] = (char)8839;
            _htmlCharacterEntities["szlig"] = (char)223;
            _htmlCharacterEntities["Tau"] = (char)932;
            _htmlCharacterEntities["tau"] = (char)964;
            _htmlCharacterEntities["there4"] = (char)8756;
            _htmlCharacterEntities["Theta"] = (char)920;
            _htmlCharacterEntities["theta"] = (char)952;
            _htmlCharacterEntities["thetasym"] = (char)977;
            _htmlCharacterEntities["thinsp"] = (char)8201;
            _htmlCharacterEntities["THORN"] = (char)222;
            _htmlCharacterEntities["thorn"] = (char)254;
            _htmlCharacterEntities["tilde"] = (char)732;
            _htmlCharacterEntities["times"] = (char)215;
            _htmlCharacterEntities["trade"] = (char)8482;
            _htmlCharacterEntities["Uacute"] = (char)218;
            _htmlCharacterEntities["uacute"] = (char)250;
            _htmlCharacterEntities["uarr"] = (char)8593;
            _htmlCharacterEntities["uArr"] = (char)8657;
            _htmlCharacterEntities["Ucirc"] = (char)219;
            _htmlCharacterEntities["ucirc"] = (char)251;
            _htmlCharacterEntities["Ugrave"] = (char)217;
            _htmlCharacterEntities["ugrave"] = (char)249;
            _htmlCharacterEntities["uml"] = (char)168;
            _htmlCharacterEntities["upsih"] = (char)978;
            _htmlCharacterEntities["Upsilon"] = (char)933;
            _htmlCharacterEntities["upsilon"] = (char)965;
            _htmlCharacterEntities["Uuml"] = (char)220;
            _htmlCharacterEntities["uuml"] = (char)252;
            _htmlCharacterEntities["weierp"] = (char)8472;
            _htmlCharacterEntities["Xi"] = (char)926;
            _htmlCharacterEntities["xi"] = (char)958;
            _htmlCharacterEntities["Yacute"] = (char)221;
            _htmlCharacterEntities["yacute"] = (char)253;
            _htmlCharacterEntities["yen"] = (char)165;
            _htmlCharacterEntities["Yuml"] = (char)376;
            _htmlCharacterEntities["yuml"] = (char)255;
            _htmlCharacterEntities["Zeta"] = (char)918;
            _htmlCharacterEntities["zeta"] = (char)950;
            _htmlCharacterEntities["zwj"] = (char)8205;
            _htmlCharacterEntities["zwnj"] = (char)8204;
        }

        #endregion Private Methods

        #region Private Fields

        // html element names
        // this is an array list now, but we may want to make it a hashtable later for better performance
        private static List<object> _htmlInlineElements;

        private static List<object> _htmlBlockElements;

        private static List<object> _htmlOtherOpenableElements;

        // list of html empty element names
        private static List<object> _htmlEmptyElements;

        // names of html elements for which closing tags are optional, and close when the outer nested element closes
        private static List<object> _htmlElementsClosingOnParentElementEnd;

        // names of elements that close certain optional closing tag elements when they start

        // names of elements closing the colgroup element
        private static List<object> _htmlElementsClosingColgroup;

        // names of elements closing the dd element
        private static List<object> _htmlElementsClosingDD;

        // names of elements closing the dt element
        private static List<object> _htmlElementsClosingDT;

        // names of elements closing the li element
        private static List<object> _htmlElementsClosingLI;

        // names of elements closing the tbody element
        private static List<object> _htmlElementsClosingTbody;

        // names of elements closing the td element
        private static List<object> _htmlElementsClosingTD;

        // names of elements closing the tfoot element
        private static List<object> _htmlElementsClosingTfoot;

        // names of elements closing the thead element
        private static List<object> _htmlElementsClosingThead;

        // names of elements closing the th element
        private static List<object> _htmlElementsClosingTH;

        // names of elements closing the tr element
        private static List<object> _htmlElementsClosingTR;

        // html character entities hashtable
        private static Dictionary<object, object> _htmlCharacterEntities;

        #endregion Private Fields
    }

    internal class HtmlParser
    {
        // ---------------------------------------------------------------------
        //
        // Constructors
        //
        // ---------------------------------------------------------------------

        #region Constructors

        /// <summary>
        /// Constructor. Initializes the _htmlLexicalAnalayzer element with the given input string
        /// </summary>
        /// <param name="inputString">
        /// string to parsed into well-formed Html
        /// </param>
        private HtmlParser(string inputString)
        {
            // Create an output xml document
            _document = new XDocument();

            // initialize open tag stack
            _openedElements = new Stack<XElement>();

            _pendingInlineElements = new Stack<XElement>();

            // initialize lexical analyzer
            _htmlLexicalAnalyzer = new HtmlLexicalAnalyzer(inputString);

            // get first token from input, expecting text
            _htmlLexicalAnalyzer.GetNextContentToken();
        }

        #endregion Constructors

        // ---------------------------------------------------------------------
        //
        // Internal Methods
        //
        // ---------------------------------------------------------------------

        #region Internal Methods

        /// <summary>
        /// Instantiates an HtmlParser element and calls the parsing function on the given input string
        /// </summary>
        /// <param name="htmlString">
        /// Input string of pssibly badly-formed Html to be parsed into well-formed Html
        /// </param>
        /// <returns>
        /// XmlElement rep
        /// </returns>
        internal static XElement ParseHtml(string htmlString)
        {
            HtmlParser htmlParser = new HtmlParser(htmlString);

            XElement htmlRootElement = htmlParser.ParseHtmlContent();

            return htmlRootElement;
        }

        // .....................................................................
        //
        // Html Header on Clipboard
        //
        // .....................................................................

        // Html header structure.
        //      Version:1.0
        //      StartHTML:000000000
        //      EndHTML:000000000
        //      StartFragment:000000000
        //      EndFragment:000000000
        //      StartSelection:000000000
        //      EndSelection:000000000
        internal const string HtmlHeader = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\nStartSelection:{4:D10}\r\nEndSelection:{5:D10}\r\n";
        internal const string HtmlStartFragmentComment = "<!--StartFragment-->";
        internal const string HtmlEndFragmentComment = "<!--EndFragment-->";

        /// <summary>
        /// Extracts Html string from clipboard data by parsing header information in htmlDataString
        /// </summary>
        /// <param name="htmlDataString">
        /// String representing Html clipboard data. This includes Html header
        /// </param>
        /// <returns>
        /// String containing only the Html data part of htmlDataString, without header
        /// </returns>
        internal static string ExtractHtmlFromClipboardData(string htmlDataString)
        {
            int startHtmlIndex = htmlDataString.IndexOf("StartHTML:");
            if (startHtmlIndex < 0)
            {
                return "ERROR: Urecognized html header";
            }
            // TODO: We assume that indices represented by strictly 10 zeros ("0123456789".Length),
            // which could be wrong assumption. We need to implement more flrxible parsing here
            startHtmlIndex = Int32.Parse(htmlDataString.Substring(startHtmlIndex + "StartHTML:".Length, "0123456789".Length));
            if (startHtmlIndex < 0 || startHtmlIndex > htmlDataString.Length)
            {
                return "ERROR: Urecognized html header";
            }

            int endHtmlIndex = htmlDataString.IndexOf("EndHTML:");
            if (endHtmlIndex < 0)
            {
                return "ERROR: Urecognized html header";
            }
            // TODO: We assume that indices represented by strictly 10 zeros ("0123456789".Length),
            // which could be wrong assumption. We need to implement more flrxible parsing here
            endHtmlIndex = Int32.Parse(htmlDataString.Substring(endHtmlIndex + "EndHTML:".Length, "0123456789".Length));
            if (endHtmlIndex > htmlDataString.Length)
            {
                endHtmlIndex = htmlDataString.Length;
            }

            return htmlDataString.Substring(startHtmlIndex, endHtmlIndex - startHtmlIndex);
        }

        /// <summary>
        /// Adds Xhtml header information to Html data string so that it can be placed on clipboard
        /// </summary>
        /// <param name="htmlString">
        /// Html string to be placed on clipboard with appropriate header
        /// </param>
        /// <returns>
        /// String wrapping htmlString with appropriate Html header
        /// </returns>
        internal static string AddHtmlClipboardHeader(string htmlString)
        {
            StringBuilder stringBuilder = new StringBuilder();

            // each of 6 numbers is represented by "{0:D10}" in the format string
            // must actually occupy 10 digit positions ("0123456789")
            int startHTML = HtmlHeader.Length + 6 * ("0123456789".Length - "{0:D10}".Length);
            int endHTML = startHTML + htmlString.Length;
            int startFragment = htmlString.IndexOf(HtmlStartFragmentComment, 0);
            if (startFragment >= 0)
            {
                startFragment = startHTML + startFragment + HtmlStartFragmentComment.Length;
            }
            else
            {
                startFragment = startHTML;
            }
            int endFragment = htmlString.IndexOf(HtmlEndFragmentComment, 0);
            if (endFragment >= 0)
            {
                endFragment = startHTML + endFragment;
            }
            else
            {
                endFragment = endHTML;
            }

            // Create HTML clipboard header string
            stringBuilder.AppendFormat(HtmlHeader, startHTML, endHTML, startFragment, endFragment, startFragment, endFragment);

            // Append HTML body.
            stringBuilder.Append(htmlString);

            return stringBuilder.ToString();
        }

        #endregion Internal Methods

        // ---------------------------------------------------------------------
        //
        // Private methods
        //
        // ---------------------------------------------------------------------

        #region Private Methods

        private void InvariantAssert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion error: " + message);
            }
        }

        /// <summary>
        /// Parses the stream of html tokens starting
        /// from the name of top-level element.
        /// Returns XmlElement representing the top-level
        /// html element
        /// </summary>
        private XElement ParseHtmlContent()
        {
            // Create artificial root elelemt to be able to group multiple top-level elements
            // We create "html" element which may be a duplicate of real HTML element, which is ok, as HtmlConverter will swallow it painlessly..
            XNamespace nsp = XhtmlNamespace;
            XElement htmlRootElement = new XElement(nsp + "html");
            OpenStructuringElement(htmlRootElement);

            while (_htmlLexicalAnalyzer.NextTokenType != HtmlTokenType.EOF)
            {
                if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.OpeningTagStart)
                {
                    _htmlLexicalAnalyzer.GetNextTagToken();
                    if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.Name)
                    {
                        string htmlElementName = _htmlLexicalAnalyzer.NextToken.ToLower();
                        _htmlLexicalAnalyzer.GetNextTagToken();

                        // Create an element
                        XElement htmlElement = new XElement(nsp + htmlElementName);

                        // Parse element attributes
                        ParseAttributes(htmlElement);

                        if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.EmptyTagEnd || HtmlSchema.IsEmptyElement(htmlElementName))
                        {
                            // It is an element without content (because of explicit slash or based on implicit knowledge aboout html)
                            AddEmptyElement(htmlElement);
                        }
                        else if (HtmlSchema.IsInlineElement(htmlElementName))
                        {
                            // Elements known as formatting are pushed to some special
                            // pending stack, which allows them to be transferred
                            // over block tags - by doing this we convert
                            // overlapping tags into normal heirarchical element structure.
                            OpenInlineElement(htmlElement);
                        }
                        else if (HtmlSchema.IsBlockElement(htmlElementName) || HtmlSchema.IsKnownOpenableElement(htmlElementName))
                        {
                            // This includes no-scope elements
                            OpenStructuringElement(htmlElement);
                        }
                        else
                        {
                            // Do nothing. Skip the whole opening tag.
                            // Ignoring all unknown elements on their start tags.
                            // Thus we will ignore them on closinng tag as well.
                            // Anyway we don't know what to do withthem on conversion to Xaml.
                        }
                    }
                    else
                    {
                        // Note that the token following opening angle bracket must be a name - lexical analyzer must guarantee that.
                        // Otherwise - we skip the angle bracket and continue parsing the content as if it is just text.
                        //  Add the following asserion here, right? or output "<" as a text run instead?:
                        // InvariantAssert(false, "Angle bracket without a following name is not expected");
                    }
                }
                else if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.ClosingTagStart)
                {
                    _htmlLexicalAnalyzer.GetNextTagToken();
                    if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.Name)
                    {
                        string htmlElementName = _htmlLexicalAnalyzer.NextToken.ToLower();

                        // Skip the name token. Assume that the following token is end of tag,
                        // but do not check this. If it is not true, we simply ignore one token
                        // - this is our recovery from bad xml in this case.
                        _htmlLexicalAnalyzer.GetNextTagToken();

                        CloseElement(htmlElementName);
                    }
                }
                else if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.Text)
                {
                    AddTextContent(_htmlLexicalAnalyzer.NextToken);
                }
                else if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.Comment)
                {
                    AddComment(_htmlLexicalAnalyzer.NextToken);
                }

                _htmlLexicalAnalyzer.GetNextContentToken();
            }

            return htmlRootElement;
        }

        private XElement CreateElementCopy(XElement htmlElement)
        {
            XNamespace nsp = XhtmlNamespace;
            XElement htmlElementCopy = new XElement(nsp + htmlElement.Name.LocalName);
            for (int i = 0; i < htmlElement.Attributes().Count(); i++)
            {
                XAttribute attribute = htmlElement.Attributes().ElementAt(i);
                htmlElementCopy.Add(attribute);
            }
            return htmlElementCopy;
        }

        private void AddEmptyElement(XElement htmlEmptyElement)
        {
            InvariantAssert(_openedElements.Count > 0, "AddEmptyElement: Stack of opened elements cannot be empty, as we have at least one artificial root element");
            XElement htmlParent = _openedElements.Peek();
            htmlParent.Add(htmlEmptyElement);
        }

        private void OpenInlineElement(XElement htmlInlineElement)
        {
            _pendingInlineElements.Push(htmlInlineElement);
        }

        // Opens structurig element such as Div or Table etc.
        private void OpenStructuringElement(XElement htmlElement)
        {
            // Close all pending inline elements
            // All block elements are considered as delimiters for inline elements
            // which forces all inline elements to be closed and re-opened in the following
            // structural element (if any).
            // By doing that we guarantee that all inline elements appear only within most nested blocks
            if (HtmlSchema.IsBlockElement(htmlElement.Name.LocalName))
            {
                while (_openedElements.Count > 0 && HtmlSchema.IsInlineElement(_openedElements.Peek().Name.LocalName))
                {
                    XElement htmlInlineElement = _openedElements.Pop();
                    InvariantAssert(_openedElements.Count > 0, "OpenStructuringElement: stack of opened elements cannot become empty here");

                    _pendingInlineElements.Push(CreateElementCopy(htmlInlineElement));
                }
            }

            // Add this block element to its parent
            if (_openedElements.Count > 0)
            {
                XElement htmlParent = _openedElements.Peek();

                // Check some known block elements for auto-closing (LI and P)
                if (HtmlSchema.ClosesOnNextElementStart(htmlParent.Name.LocalName, htmlElement.Name.LocalName))
                {
                    _openedElements.Pop();
                    htmlParent = _openedElements.Count > 0 ? _openedElements.Peek() : null;
                }

                if (htmlParent != null)
                {
                    // NOTE:
                    // Actually we never expect null - it would mean two top-level P or LI (without a parent).
                    // In such weird case we will loose all paragraphs except the first one...
                    htmlParent.Add(htmlElement);
                }
            }

            // Push it onto a stack
            _openedElements.Push(htmlElement);
        }

        private bool IsElementOpened(string htmlElementName)
        {
            foreach (XElement openedElement in _openedElements)
            {
                if (openedElement.Name.LocalName == htmlElementName)
                {
                    return true;
                }
            }
            return false;
        }

        private void CloseElement(string htmlElementName)
        {
            // Check if the element is opened and already added to the parent
            InvariantAssert(_openedElements.Count > 0, "CloseElement: Stack of opened elements cannot be empty, as we have at least one artificial root element");

            // Check if the element is opened and still waiting to be added to the parent
            if (_pendingInlineElements.Count > 0 && _pendingInlineElements.Peek().Name.LocalName == htmlElementName)
            {
                // Closing an empty inline element.
                // Note that HtmlConverter will skip empty inlines, but for completeness we keep them here on parser level.
                XElement htmlInlineElement = _pendingInlineElements.Pop();
                InvariantAssert(_openedElements.Count > 0, "CloseElement: Stack of opened elements cannot be empty, as we have at least one artificial root element");
                XElement htmlParent = _openedElements.Peek();
                htmlParent.Add(htmlInlineElement);
                return;
            }
            else if (IsElementOpened(htmlElementName))
            {
                while (_openedElements.Count > 1) // we never pop the last element - the artificial root
                {
                    // Close all unbalanced elements.
                    XElement htmlOpenedElement = _openedElements.Pop();

                    if (htmlOpenedElement.Name.LocalName == htmlElementName)
                    {
                        return;
                    }

                    if (HtmlSchema.IsInlineElement(htmlOpenedElement.Name.LocalName))
                    {
                        // Unbalances Inlines will be transfered to the next element content
                        _pendingInlineElements.Push(CreateElementCopy(htmlOpenedElement));
                    }
                }
            }

            // If element was not opened, we simply ignore the unbalanced closing tag
            return;
        }

        private void AddTextContent(string textContent)
        {
            OpenPendingInlineElements();

            InvariantAssert(_openedElements.Count > 0, "AddTextContent: Stack of opened elements cannot be empty, as we have at least one artificial root element");

            XElement htmlParent = _openedElements.Peek();
            XText textNode = new XText(textContent);
            htmlParent.Add(textNode);
        }

        private void AddComment(string comment)
        {
            /*OpenPendingInlineElements();

            InvariantAssert(_openedElements.Count > 0, "AddComment: Stack of opened elements cannot be empty, as we have at least one artificial root element");

            XmlElement htmlParent = _openedElements.Peek();
            XmlComment xmlComment = _document.CreateComment(comment);
            htmlParent.AppendChild(xmlComment);
             */
        }

        // Moves all inline elements pending for opening to actual document
        // and adds them to current open stack.
        private void OpenPendingInlineElements()
        {
            if (_pendingInlineElements.Count > 0)
            {
                XElement htmlInlineElement = _pendingInlineElements.Pop();

                OpenPendingInlineElements();

                InvariantAssert(_openedElements.Count > 0, "OpenPendingInlineElements: Stack of opened elements cannot be empty, as we have at least one artificial root element");

                XElement htmlParent = _openedElements.Peek();
                htmlParent.Add(htmlInlineElement);
                _openedElements.Push(htmlInlineElement);
            }
        }

        private void ParseAttributes(XElement xmlElement)
        {
            while (_htmlLexicalAnalyzer.NextTokenType != HtmlTokenType.EOF && //
                _htmlLexicalAnalyzer.NextTokenType != HtmlTokenType.TagEnd && //
                _htmlLexicalAnalyzer.NextTokenType != HtmlTokenType.EmptyTagEnd)
            {
                // read next attribute (name=value)
                if (_htmlLexicalAnalyzer.NextTokenType == HtmlTokenType.Name)
                {
                    string attributeName = _htmlLexicalAnalyzer.NextToken;
                    _htmlLexicalAnalyzer.GetNextEqualSignToken();

                    _htmlLexicalAnalyzer.GetNextAtomToken();

                    string attributeValue = _htmlLexicalAnalyzer.NextToken;
                    xmlElement.Add(new XAttribute(attributeName, attributeValue));
                }
                _htmlLexicalAnalyzer.GetNextTagToken();
            }
        }

        #endregion Private Methods


        // ---------------------------------------------------------------------
        //
        // Private Fields
        //
        // ---------------------------------------------------------------------

        #region Private Fields

        internal const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

        private HtmlLexicalAnalyzer _htmlLexicalAnalyzer;

        // document from which all elements are created
        private XDocument _document;

        // stack for open elements
        Stack<XElement> _openedElements;
        Stack<XElement> _pendingInlineElements;

        #endregion Private Fields
    }

    public static class HtmlToXamlConverter
    {
        // ---------------------------------------------------------------------
        //
        // Internal Methods
        //
        // ---------------------------------------------------------------------

        #region Internal Methods

        public static XElement ConvertHtmlToXml(string htmlString)
        {
            // Create well-formed Xml from Html string
            return HtmlParser.ParseHtml(htmlString);
        }

        /// <summary>
        /// Converts an html string into xaml string.
        /// </summary>
        /// <param name="htmlString">
        /// Input html which may be badly formated xml.
        /// </param>
        /// <param name="asFlowDocument">
        /// true indicates that we need a FlowDocument as a root element;
        /// false means that Section or Span elements will be used
        /// dependeing on StartFragment/EndFragment comments locations.
        /// </param>
        /// <returns>
        /// Well-formed xml representing XAML equivalent for the input html string.
        /// </returns>
        public static string ConvertHtmlToXaml(string htmlString, bool asFlowDocument, int blockMaxSize)
        {
            // Create well-formed Xml from Html string
            XElement htmlElement = ConvertHtmlToXml(htmlString);
            return ConvertXmlToXaml(htmlElement, asFlowDocument, blockMaxSize);
        }

        public static string ConvertXmlToXaml(XElement htmlElement, bool asFlowDocument, int blockMaxSize)
        {
            // Decide what name to use as a root
            string rootElementName = asFlowDocument ? HtmlToXamlConverter.Xaml_FlowDocument : HtmlToXamlConverter.Xaml_Section;

            // Create an XmlDocument for generated xaml
            XDocument xamlTree = new XDocument();
            XNamespace nsp = _xamlNamespace;
            XElement xamlFlowDocumentElement = new XElement(nsp + rootElementName);

            // Source context is a stack of all elements - ancestors of a parentElement
            List<XElement> sourceContext = new List<XElement>(10);

            // Clear fragment parent
            InlineFragmentParentElement = null;

            int blockSize = 0;
            XElement htmlBlock = new XElement("html");
            foreach (var node in htmlElement.Nodes())
            {
                if (node is XElement)
                {
                    string nodeValue = (node as XElement).Value;
                    if (nodeValue.Length > blockMaxSize)
                    {
                        string newValue = nodeValue.Substring(0, blockMaxSize);
                        (node as XElement).Value = newValue.Substring(0, newValue.LastIndexOf(" "));
                        nodeValue = nodeValue.Substring(newValue.LastIndexOf(" ") + 1);
                        XElement newNode = new XElement("p", nodeValue, (node as XElement).Attribute("class"));
                        node.AddAfterSelf(newNode);
                    }
                }
            }

            foreach (var node in htmlElement.Nodes())
            {
                XText nodeTxt = node as XText;
                if (nodeTxt != null && nodeTxt.Value.Trim().Length == 0)
                {
                    continue;
                }

                XElement nodeElem = node as XElement;
                if (nodeElem != null && nodeElem.Value == "")
                {
                    continue;
                }

                if (blockSize + node.ToString().Length > blockMaxSize)
                {
                    AddBlock(xamlFlowDocumentElement, htmlBlock, new Dictionary<object, object>(), sourceContext);
                    htmlBlock = new XElement("html");
                    blockSize = 0;
                }

                htmlBlock.Add(node);
                blockSize += node.ToString().Length;
            }

            // convert root html element
            AddBlock(xamlFlowDocumentElement, htmlBlock, new Dictionary<object, object>(), sourceContext);
//            AddBlock(xamlFlowDocumentElement, htmlElement, new Dictionary<object, object>(), sourceContext);

            // In case if the selected fragment is inline, extract it into a separate Span wrapper
            if (!asFlowDocument)
            {
                xamlFlowDocumentElement = ExtractInlineFragment(xamlFlowDocumentElement);
            }

            // Return a string representing resulting Xaml
            xamlFlowDocumentElement.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));

            return xamlFlowDocumentElement.ToString();
        }

        /// <summary>
        /// Returns a value for an attribute by its name (ignoring casing)
        /// </summary>
        /// <param name="element">
        /// XmlElement in which we are trying to find the specified attribute
        /// </param>
        /// <param name="attributeName">
        /// String representing the attribute name to be searched for
        /// </param>
        /// <returns></returns>
        public static string GetAttribute(XElement element, string attributeName)
        {
            attributeName = attributeName.ToLower();

            for (int i = 0; i < element.Attributes().Count(); i++)
            {
                if (element.Attributes().ToArray()[i].Name.LocalName.ToLower() == attributeName)
                {
                    return element.Attributes().ToArray()[i].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns string extracted from quotation marks
        /// </summary>
        /// <param name="value">
        /// String representing value enclosed in quotation marks
        /// </param>
        internal static string UnQuote(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\"") || value.StartsWith("'") && value.EndsWith("'"))
            {
                value = value.Substring(1, value.Length - 2).Trim();
            }
            return value;
        }

        #endregion Internal Methods

        // ---------------------------------------------------------------------
        //
        // Private Methods
        //
        // ---------------------------------------------------------------------

        #region Private Methods

        /// <summary>
        /// Analyzes the given htmlElement expecting it to be converted
        /// into some of xaml Block elements and adds the converted block
        /// to the children collection of xamlParentElement.
        ///
        /// Analyzes the given XmlElement htmlElement, recognizes it as some HTML element
        /// and adds it as a child to a xamlParentElement.
        /// In some cases several following siblings of the given htmlElement
        /// will be consumed too (e.g. LIs encountered without wrapping UL/OL,
        /// which must be collected together and wrapped into one implicit List element).
        /// </summary>
        /// <param name="xamlParentElement">
        /// Parent xaml element, to which new converted element will be added
        /// </param>
        /// <param name="htmlElement">
        /// Source html element subject to convert to xaml.
        /// </param>
        /// <param name="inheritedProperties">
        /// Properties inherited from an outer context.
        /// </param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        /// <returns>
        /// Last processed html node. Normally it should be the same htmlElement
        /// as was passed as a paramater, but in some irregular cases
        /// it could one of its following siblings.
        /// The caller must use this node to get to next sibling from it.
        /// </returns>
        private static XNode AddBlock(XElement xamlParentElement, XNode htmlNode, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            if (htmlNode is XComment)
            {
                DefineInlineFragmentParent((XComment)htmlNode, null);
            }
            else if (htmlNode is XText)
            {
                htmlNode = AddImplicitParagraph(xamlParentElement, htmlNode, inheritedProperties, sourceContext);
            }
            else if (htmlNode is XElement)
            {
                // Identify element name
                XElement htmlElement = (XElement)htmlNode;

                string htmlElementName = htmlElement.Name.LocalName; // Keep the name case-sensitive to check xml names

                // Put source element to the stack
                sourceContext.Add(htmlElement);

                // Convert the name to lowercase, because html elements are case-insensitive
                htmlElementName = htmlElementName.ToLower();

                // Switch to an appropriate kind of processing depending on html element name
                switch (htmlElementName)
                {
                    // Sections:
                    case "html":
                    case "body":
                    case "div":
                        {
                            var classValue = htmlElement.Attribute("class");
                            if (classValue != null && classValue.Value == "include-text")
                            {
                                //skip the include text
                            }
                            else
                            {
                                AddSection(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                            }
                        }
                        break;
                    case "form": // not a block according to xhtml spec
                    case "pre": // Renders text in a fixed-width font
                    case "blockquote":
                    case "caption":
                    case "center":
                    case "cite":
                        AddSection(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;

                    // Paragraphs:
                    case "p":
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                    case "nsrtitle":
                    case "textarea":
                    case "dd": // ???
                    case "dl": // ???
                    case "dt": // ???
                    case "tt": // ???
                        AddParagraph(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;

                    case "ol":
                    case "ul":
                    case "dir": //  treat as UL element
                    case "menu": //  treat as UL element
                        // List element conversion
                        AddList(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;
                    case "li":
                        // LI outside of OL/UL
                        // Collect all sibling LIs, wrap them into a List and then proceed with the element following the last of LIs
                        htmlNode = AddOrphanListItems(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;

                    case "img":
                        // TODO: Add image processing
                        AddImage(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;

                    case "table":
                        // hand off to table parsing function which will perform special table syntax checks
                        //AddTable(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;

                    case "tbody":
                    case "tfoot":
                    case "thead":
                    case "tr":
                    case "td":
                    case "th":
                        // Table stuff without table wrapper
                        // TODO: add special-case processing here for elements that should be within tables when the
                        // parent element is NOT a table. If the parent element is a table they can be processed normally.
                        // we need to compare against the parent element here, we can't just break on a switch
                        goto default; // Thus we will skip this element as unknown, but still recurse into it.

                    case "style": // We already pre-processed all style elements. Ignore it now
                    case "meta":
                    case "head":
                    case "title":
                    case "script":
                        // Ignore these elements
                        break;

                    default:
                        // Wrap a sequence of inlines into an implicit paragraph
                        htmlNode = AddImplicitParagraph(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;
                }

                // Remove the element from the stack
                Debug.Assert(sourceContext.Count > 0 && sourceContext[sourceContext.Count - 1] == htmlElement);
                sourceContext.RemoveAt(sourceContext.Count - 1);
            }

            // Return last processed node
            return htmlNode;
        }

        // .............................................................
        //
        // Line Breaks
        //
        // .............................................................

        private static void AddBreak(XElement xamlParentElement, string htmlElementName)
        {
            // Create new xaml element corresponding to this html element
            XNamespace nsp = _xamlNamespace;
            XElement xamlLineBreak = new XElement(nsp + HtmlToXamlConverter.Xaml_LineBreak);
            xamlParentElement.Add(xamlLineBreak);
            if (htmlElementName == "hr")
            {
                XText xamlHorizontalLine = new XText("----------------------");
                xamlParentElement.Add(xamlHorizontalLine);
                xamlLineBreak = new XElement(nsp + HtmlToXamlConverter.Xaml_LineBreak);
                xamlParentElement.Add(xamlLineBreak);
            }
        }

        // .............................................................
        //
        // Text Flow Elements
        //
        // .............................................................

        /// <summary>
        /// Generates Section or Paragraph element from DIV depending whether it contains any block elements or not
        /// </summary>
        /// <param name="xamlParentElement">
        /// XmlElement representing Xaml parent to which the converted element should be added
        /// </param>
        /// <param name="htmlElement">
        /// XmlElement representing Html element to be converted
        /// </param>
        /// <param name="inheritedProperties">
        /// properties inherited from parent context
        /// </param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        /// true indicates that a content added by this call contains at least one block element
        /// </param>
        private static void AddSection(XElement xamlParentElement, XElement htmlElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            // Analyze the content of htmlElement to decide what xaml element to choose - Section or Paragraph.
            // If this Div has at least one block child then we need to use Section, otherwise use Paragraph
            bool htmlElementContainsBlocks = false;
            for (XNode htmlChildNode = htmlElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode.NextNode)
            {
                if (htmlChildNode is XElement)
                {
                    string htmlChildName = ((XElement)htmlChildNode).Name.LocalName.ToLower();
                    if (HtmlSchema.IsBlockElement(htmlChildName))
                    {
                        htmlElementContainsBlocks = true;
                        break;
                    }
                }
            }

            if (!htmlElementContainsBlocks)
            {
                // The Div does not contain any block elements, so we can treat it as a Paragraph
                AddParagraph(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
            }
            else
            {
                // The Div has some nested blocks, so we treat it as a Section

                // Create currentProperties as a compilation of local and inheritedProperties, set localProperties
                Dictionary<object, object> localProperties;
                Dictionary<object, object> currentProperties = GetElementProperties(htmlElement, inheritedProperties, out localProperties, sourceContext);

                // Create a XAML element corresponding to this html element
                XNamespace nsp = _xamlNamespace;
                XElement xamlElement = new XElement(nsp + HtmlToXamlConverter.Xaml_Section);
                ApplyLocalProperties(xamlElement, localProperties, true);

                // Decide whether we can unwrap this element as not having any formatting significance.
                if (!xamlElement.HasAttributes)
                {
                    // This elements is a group of block elements whitout any additional formatting.
                    // We can add blocks directly to xamlParentElement and avoid
                    // creating unnecessary Sections nesting.
                    xamlElement = xamlParentElement;
                }

                // Recurse into element subtree
                for (XNode htmlChildNode = htmlElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode != null ? htmlChildNode.NextNode : null)
                {
                    htmlChildNode = AddBlock(xamlElement, htmlChildNode, currentProperties, sourceContext);
                }

                // Add the new element to the parent.
                if (xamlElement != xamlParentElement)
                {
                    xamlParentElement.Add(xamlElement);
                }
            }
        }

        /// <summary>
        /// Generates Paragraph element from P, H1-H7, Center etc.
        /// </summary>
        /// <param name="xamlParentElement">
        /// XmlElement representing Xaml parent to which the converted element should be added
        /// </param>
        /// <param name="htmlElement">
        /// XmlElement representing Html element to be converted
        /// </param>
        /// <param name="inheritedProperties">
        /// properties inherited from parent context
        /// </param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        /// true indicates that a content added by this call contains at least one block element
        /// </param>
        private static void AddParagraph(XElement xamlParentElement, XElement htmlElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            // Create currentProperties as a compilation of local and inheritedProperties, set localProperties
            Dictionary<object, object> localProperties;
            Dictionary<object, object> currentProperties = GetElementProperties(htmlElement, inheritedProperties, out localProperties, sourceContext);

            // Create a XAML element corresponding to this html element
            XNamespace nsp = _xamlNamespace;
            XElement xamlElement = new XElement(nsp + HtmlToXamlConverter.Xaml_Paragraph);
            ApplyLocalProperties(xamlElement, localProperties, true);

            // Recurse into element subtree
            for (XNode htmlChildNode = htmlElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode.NextNode)
            {
                AddInline(xamlElement, htmlChildNode, currentProperties, sourceContext);
            }

            // Add the new element to the parent.
            xamlParentElement.Add(xamlElement);
        }

        /// <summary>
        /// Creates a Paragraph element and adds all nodes starting from htmlNode
        /// converted to appropriate Inlines.
        /// </summary>
        /// <param name="xamlParentElement">
        /// XmlElement representing Xaml parent to which the converted element should be added
        /// </param>
        /// <param name="htmlNode">
        /// XmlNode starting a collection of implicitly wrapped inlines.
        /// </param>
        /// <param name="inheritedProperties">
        /// properties inherited from parent context
        /// </param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        /// true indicates that a content added by this call contains at least one block element
        /// </param>
        /// <returns>
        /// The last htmlNode added to the implicit paragraph
        /// </returns>
        private static XNode AddImplicitParagraph(XElement xamlParentElement, XNode htmlNode, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            // Collect all non-block elements and wrap them into implicit Paragraph
            XNamespace nsp = _xamlNamespace;
            XElement xamlParagraph = new XElement(nsp + HtmlToXamlConverter.Xaml_Paragraph);
            XNode lastNodeProcessed = null;
            while (htmlNode != null)
            {
                if (htmlNode is XComment)
                {
                    DefineInlineFragmentParent((XComment)htmlNode, null);
                }
                else if (htmlNode is XText)
                {
                    if ((htmlNode as XText).Value.Trim().Length > 0)
                    {
                        AddTextRun(xamlParagraph, (htmlNode as XText).Value);
                    }
                }
                else if (htmlNode is XElement)
                {
                    string htmlChildName = ((XElement)htmlNode).Name.LocalName.ToLower();
                    if (HtmlSchema.IsBlockElement(htmlChildName))
                    {
                        // The sequence of non-blocked inlines ended. Stop implicit loop here.
                        break;
                    }
                    else
                    {
                        AddInline(xamlParagraph, (XElement)htmlNode, inheritedProperties, sourceContext);
                    }
                }

                // Store last processed node to return it at the end
                lastNodeProcessed = htmlNode;
                htmlNode = htmlNode.NextNode;
            }

            // Add the Paragraph to the parent
            // If only whitespaces and commens have been encountered,
            // then we have nothing to add in implicit paragraph; forget it.
            if (xamlParagraph.FirstNode != null)
            {
                xamlParentElement.Add(xamlParagraph);
            }

            // Need to return last processed node
            return lastNodeProcessed;
        }

        // .............................................................
        //
        // Inline Elements
        //
        // .............................................................

        private static void AddInline(XElement xamlParentElement, XNode htmlNode, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            if (htmlNode is XComment)
            {
                DefineInlineFragmentParent((XComment)htmlNode, xamlParentElement);
            }
            else if (htmlNode is XText)
            {
                AddTextRun(xamlParentElement, (htmlNode as XText).Value);
            }
            else if (htmlNode is XElement)
            {
                XElement htmlElement = (XElement)htmlNode;

                // Identify element name
                string htmlElementName = htmlElement.Name.LocalName.ToLower();

                // Put source element to the stack
                sourceContext.Add(htmlElement);

                switch (htmlElementName)
                {
                    case "a":
                        AddHyperlink(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;
                    case "img":
                        AddImage(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        break;
                    case "br":
                    case "hr":
                        //AddBreak(xamlParentElement, htmlElementName);
                        break;
                    default:
                        if (HtmlSchema.IsInlineElement(htmlElementName) || HtmlSchema.IsBlockElement(htmlElementName))
                        {
                            // Note: actually we do not expect block elements here,
                            // but if it happens to be here, we will treat it as a Span.

                            AddSpanOrRun(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
                        }
                        break;
                }
                // Ignore all other elements non-(block/inline/image)

                // Remove the element from the stack
                Debug.Assert(sourceContext.Count > 0 && sourceContext[sourceContext.Count - 1] == htmlElement);
                sourceContext.RemoveAt(sourceContext.Count - 1);
            }
        }

        private static void AddSpanOrRun(XElement xamlParentElement, XElement htmlElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            // Decide what XAML element to use for this inline element.
            // Check whether it contains any nested inlines
            bool elementHasChildren = false;
            for (XNode htmlNode = htmlElement.FirstNode; htmlNode != null; htmlNode = htmlNode.NextNode)
            {
                if (htmlNode is XElement)
                {
                    string htmlChildName = ((XElement)htmlNode).Name.LocalName.ToLower();
                    if (HtmlSchema.IsInlineElement(htmlChildName) || HtmlSchema.IsBlockElement(htmlChildName) ||
                        htmlChildName == "img" || htmlChildName == "br" || htmlChildName == "hr")
                    {
                        elementHasChildren = true;
                        break;
                    }
                }
            }

            string xamlElementName = elementHasChildren ? HtmlToXamlConverter.Xaml_Span : HtmlToXamlConverter.Xaml_Run;

            // Create currentProperties as a compilation of local and inheritedProperties, set localProperties
            Dictionary<object, object> localProperties;
            Dictionary<object, object> currentProperties = GetElementProperties(htmlElement, inheritedProperties, out localProperties, sourceContext);

            // Create a XAML element corresponding to this html element
            XNamespace nsp = _xamlNamespace;
            XElement xamlElement = new XElement(nsp + xamlElementName);
            ApplyLocalProperties(xamlElement, localProperties, false);

            // Recurse into element subtree
            for (XNode htmlChildNode = htmlElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode.NextNode)
            {
                AddInline(xamlElement, htmlChildNode, currentProperties, sourceContext);
            }

            // Add the new element to the parent.
            xamlParentElement.Add(xamlElement);
        }

        // Adds a text run to a xaml tree
        private static void AddTextRun(XElement xamlElement, string textData)
        {
            // Remove control characters
            for (int i = 0; i < textData.Length; i++)
            {
                if (Char.IsControl(textData[i]))
                {
                    textData = textData.Remove(i--, 1);  // decrement i to compensate for character removal
                }
            }

            // Replace No-Breaks by spaces (160 is a code of &nbsp; entity in html)
            //  This is a work around since WPF/XAML does not support &nbsp.
            textData = textData.Replace((char)160, ' ');

            if (textData.Length > 0)
            {
                xamlElement.Add(new XText(textData));
            }
        }

        private static void AddHyperlink(XElement xamlParentElement, XElement htmlElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            // Convert href attribute into NavigateUri and TargetName
            string href = GetAttribute(htmlElement, "href");
            if (href == null)
            {
                // When href attribute is missing - ignore the hyperlink
                AddSpanOrRun(xamlParentElement, htmlElement, inheritedProperties, sourceContext);
            }
            else
            {
                // Create currentProperties as a compilation of local and inheritedProperties, set localProperties
                Dictionary<object, object> localProperties;
                Dictionary<object, object> currentProperties = GetElementProperties(htmlElement, inheritedProperties, out localProperties, sourceContext);

                // Create a XAML element corresponding to this html element
                XNamespace nsp = _xamlNamespace;
                XElement xamlElement = new XElement(nsp + HtmlToXamlConverter.Xaml_Hyperlink);
                ApplyLocalProperties(xamlElement, localProperties, false);

                string[] hrefParts = href.Split(new char[] { '#' });
                if (hrefParts.Length > 0 && hrefParts[0].Trim().Length > 0)
                {
                    xamlElement.Add(new XAttribute(HtmlToXamlConverter.Xaml_Hyperlink_NavigateUri, hrefParts[0].Trim()));
                }

                if (hrefParts.Length == 2 && hrefParts[1].Trim().Length > 0)
                {
                    xamlElement.Add(new XAttribute(HtmlToXamlConverter.Xaml_Hyperlink_TargetName, hrefParts[1].Trim()));
                }

                // Recurse into element subtree
                for (XNode htmlChildNode = htmlElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode.NextNode)
                {
                    AddInline(xamlElement, htmlChildNode, currentProperties, sourceContext);
                }

                // Add the new element to the parent.
                xamlParentElement.Add(xamlElement);
            }
        }

        // Stores a parent xaml element for the case when selected fragment is inline.
        private static XElement InlineFragmentParentElement;

        // Called when html comment is encountered to store a parent element
        // for the case when the fragment is inline - to extract it to a separate
        // Span wrapper after the conversion.
        private static void DefineInlineFragmentParent(XComment htmlComment, XElement xamlParentElement)
        {
            if (htmlComment.Value == "StartFragment")
            {
                InlineFragmentParentElement = xamlParentElement;
            }
            else if (htmlComment.Value == "EndFragment")
            {
                if (InlineFragmentParentElement == null && xamlParentElement != null)
                {
                    // Normally this cannot happen if comments produced by correct copying code
                    // in Word or IE, but when it is produced manually then fragment boundary
                    // markers can be inconsistent. In this case StartFragment takes precedence,
                    // but if it is not set, then we get the value from EndFragment marker.
                    InlineFragmentParentElement = xamlParentElement;
                }
            }
        }

        // Extracts a content of an element stored as InlineFragmentParentElement
        // into a separate Span wrapper.
        // Note: when selected content does not cross paragraph boundaries,
        // the fragment is marked within
        private static XElement ExtractInlineFragment(XElement xamlFlowDocumentElement)
        {
            if (InlineFragmentParentElement != null)
            {
                if (InlineFragmentParentElement.Name.LocalName == HtmlToXamlConverter.Xaml_Span)
                {
                    xamlFlowDocumentElement = InlineFragmentParentElement;
                }
                else
                {
                    XNamespace nsp = _xamlNamespace;
                    xamlFlowDocumentElement = new XElement(nsp + HtmlToXamlConverter.Xaml_Span);
                    while (InlineFragmentParentElement.FirstNode != null)
                    {
                        XNode copyNode = InlineFragmentParentElement.FirstNode;
                        copyNode.Remove();
                        xamlFlowDocumentElement.Add(copyNode);
                    }
                }
            }

            return xamlFlowDocumentElement;
        }

        // .............................................................
        //
        // Images
        //
        // .............................................................

        private static void AddImage(XElement xamlParentElement, XElement htmlElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            //  Implement images
        }

        // .............................................................
        //
        // Lists
        //
        // .............................................................

        /// <summary>
        /// Converts Html ul or ol element into Xaml list element. During conversion if the ul/ol element has any children
        /// that are not li elements, they are ignored and not added to the list element
        /// </summary>
        /// <param name="xamlParentElement">
        /// XmlElement representing Xaml parent to which the converted element should be added
        /// </param>
        /// <param name="htmlListElement">
        /// XmlElement representing Html ul/ol element to be converted
        /// </param>
        /// <param name="inheritedProperties">
        /// properties inherited from parent context
        /// </param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        private static void AddList(XElement xamlParentElement, XElement htmlListElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            string htmlListElementName = htmlListElement.Name.LocalName.ToLower();

            Dictionary<object, object> localProperties;
            Dictionary<object, object> currentProperties = GetElementProperties(htmlListElement, inheritedProperties, out localProperties, sourceContext);

            // Create Xaml List element
            XNamespace nsp = _xamlNamespace;
            XElement xamlListElement = new XElement(nsp + Xaml_List);

            // Set default list markers
            if (htmlListElementName == "ol")
            {
                // Ordered list
                xamlListElement.Add(new XAttribute(HtmlToXamlConverter.Xaml_List_MarkerStyle, Xaml_List_MarkerStyle_Decimal));
            }
            else
            {
                // Unordered list - all elements other than OL treated as unordered lists
                xamlListElement.Add(new XAttribute(HtmlToXamlConverter.Xaml_List_MarkerStyle, Xaml_List_MarkerStyle_Disc));
            }

            // Apply local properties to list to set marker attribute if specified
            // TODO: Should we have separate list attribute processing function?
            ApplyLocalProperties(xamlListElement, localProperties, true);

            // Recurse into list subtree
            for (XNode htmlChildNode = htmlListElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode.NextNode)
            {
                if (htmlChildNode is XElement && (htmlChildNode as XElement).Name.LocalName.ToLower() == "li")
                {
                    sourceContext.Add((XElement)htmlChildNode);
                    AddListItem(xamlListElement, (XElement)htmlChildNode, currentProperties, sourceContext);
                    Debug.Assert(sourceContext.Count > 0 && sourceContext[sourceContext.Count - 1] == htmlChildNode);
                    sourceContext.RemoveAt(sourceContext.Count - 1);
                }
                else
                {
                    // Not an li element. Add it to previous ListBoxItem
                    //  We need to append the content to the end
                    // of a previous list item.
                }
            }

            // Add the List element to xaml tree - if it is not empty
            if (xamlListElement.Elements().Count() > 0)
            {
                xamlParentElement.Add(xamlListElement);
            }
        }

        /// <summary>
        /// If li items are found without a parent ul/ol element in Html string, creates xamlListElement as their parent and adds
        /// them to it. If the previously added node to the same xamlParentElement was a List, adds the elements to that list.
        /// Otherwise, we create a new xamlListElement and add them to it. Elements are added as long as li elements appear sequentially.
        /// The first non-li or text node stops the addition.
        /// </summary>
        /// <param name="xamlParentElement">
        /// Parent element for the list
        /// </param>
        /// <param name="htmlLIElement">
        /// Start Html li element without parent list
        /// </param>
        /// <param name="inheritedProperties">
        /// Properties inherited from parent context
        /// </param>
        /// <returns>
        /// XmlNode representing the first non-li node in the input after one or more li's have been processed.
        /// </returns>
        private static XElement AddOrphanListItems(XElement xamlParentElement, XElement htmlLIElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            Debug.Assert(htmlLIElement.Name.LocalName.ToLower() == "li");

            XElement lastProcessedListItemElement = null;

            // Find out the last element attached to the xamlParentElement, which is the previous sibling of this node
            XNode xamlListItemElementPreviousSibling = xamlParentElement.Elements().Last();
            XElement xamlListElement;
            if (xamlListItemElementPreviousSibling != null && (xamlListItemElementPreviousSibling as XElement).Name.LocalName == Xaml_List)
            {
                // Previously added Xaml element was a list. We will add the new li to it
                xamlListElement = (XElement)xamlListItemElementPreviousSibling;
            }
            else
            {
                // No list element near. Create our own.
                XNamespace nsp = _xamlNamespace;
                xamlListElement = new XElement(nsp + Xaml_List);
                xamlParentElement.Add(xamlListElement);
            }

            XNode htmlChildNode = htmlLIElement;
            string htmlChildNodeName = htmlChildNode == null ? null : (htmlChildNode as XElement).Name.LocalName.ToLower();

            //  Current element properties missed here.
            //currentProperties = GetElementProperties(htmlLIElement, inheritedProperties, out localProperties, stylesheet);

            // Add li elements to the parent xamlListElement we created as long as they appear sequentially
            // Use properties inherited from xamlParentElement for context
            while (htmlChildNode != null && htmlChildNodeName == "li")
            {
                AddListItem(xamlListElement, (XElement)htmlChildNode, inheritedProperties, sourceContext);
                lastProcessedListItemElement = (XElement)htmlChildNode;
                htmlChildNode = htmlChildNode.NextNode;
                htmlChildNodeName = htmlChildNode == null ? null : (htmlChildNode as XElement).Name.LocalName.ToLower();
            }

            return lastProcessedListItemElement;
        }

        /// <summary>
        /// Converts htmlLIElement into Xaml ListItem element, and appends it to the parent xamlListElement
        /// </summary>
        /// <param name="xamlListElement">
        /// XmlElement representing Xaml List element to which the converted td/th should be added
        /// </param>
        /// <param name="htmlLIElement">
        /// XmlElement representing Html li element to be converted
        /// </param>
        /// <param name="inheritedProperties">
        /// Properties inherited from parent context
        /// </param>
        private static void AddListItem(XElement xamlListElement, XElement htmlLIElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            // Parameter validation
            Debug.Assert(xamlListElement != null);
            Debug.Assert(xamlListElement.Name == Xaml_List);
            Debug.Assert(htmlLIElement != null);
            Debug.Assert(htmlLIElement.Name.LocalName.ToLower() == "li");
            Debug.Assert(inheritedProperties != null);

            Dictionary<object, object> localProperties;
            Dictionary<object, object> currentProperties = GetElementProperties(htmlLIElement, inheritedProperties, out localProperties, sourceContext);

            XNamespace nsp = _xamlNamespace;
            XElement xamlListItemElement = new XElement(nsp + Xaml_ListItem);

            // TODO: process local properties for li element

            // Process children of the ListItem
            for (XNode htmlChildNode = htmlLIElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode != null ? htmlChildNode.NextNode : null)
            {
                htmlChildNode = AddBlock(xamlListItemElement, htmlChildNode, currentProperties, sourceContext);
            }

            // Add resulting ListBoxItem to a xaml parent
            xamlListElement.Add(xamlListItemElement);
        }

        // .............................................................
        //
        // Tables
        //
        // .............................................................

        /// <summary>
        /// Converts htmlTableElement to a Xaml Table element. Adds tbody elements if they are missing so
        /// that a resulting Xaml Table element is properly formed.
        /// </summary>
        /// <param name="xamlParentElement">
        /// Parent xaml element to which a converted table must be added.
        /// </param>
        /// <param name="htmlTableElement">
        /// XmlElement reprsenting the Html table element to be converted
        /// </param>
        /// <param name="inheritedProperties">
        /// Hashtable representing properties inherited from parent context.
        /// </param>
        private static void AddTable(XElement xamlParentElement, XElement htmlTableElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            // Parameter validation
            Debug.Assert(htmlTableElement.Name.LocalName.ToLower() == "table");
            Debug.Assert(xamlParentElement != null);
            Debug.Assert(inheritedProperties != null);

            // Create current properties to be used by children as inherited properties, set local properties
            Dictionary<object, object> localProperties;
            Dictionary<object, object> currentProperties = GetElementProperties(htmlTableElement, inheritedProperties, out localProperties, sourceContext);

            // TODO: process localProperties for tables to override defaults, decide cell spacing defaults

            // Check if the table contains only one cell - we want to take only its content
            XElement singleCell = GetCellFromSingleCellTable(htmlTableElement);

            if (singleCell != null)
            {
                //  Need to push skipped table elements onto sourceContext
                sourceContext.Add(singleCell);

                // Add the cell's content directly to parent
                for (XNode htmlChildNode = singleCell.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode != null ? htmlChildNode.NextNode : null)
                {
                    htmlChildNode = AddBlock(xamlParentElement, htmlChildNode, currentProperties, sourceContext);
                }

                Debug.Assert(sourceContext.Count > 0 && sourceContext[sourceContext.Count - 1] == singleCell);
                sourceContext.RemoveAt(sourceContext.Count - 1);
            }
            else
            {
                // Create xamlTableElement
                XNamespace nsp = _xamlNamespace;
                XElement xamlTableElement = new XElement(nsp + Xaml_Table);

                // Analyze table structure for column widths and rowspan attributes
                List<object> columnStarts = AnalyzeTableStructure(htmlTableElement);

                // Process COLGROUP & COL elements
                AddColumnInformation(htmlTableElement, xamlTableElement, columnStarts, currentProperties, sourceContext);

                // Process table body - TBODY and TR elements
                XNode htmlChildNode = htmlTableElement.FirstNode;

                while (htmlChildNode != null)
                {
                    string htmlChildName = (htmlChildNode as XElement).Name.LocalName.ToLower();

                    // Process the element
                    if (htmlChildName == "tbody" || htmlChildName == "thead" || htmlChildName == "tfoot")
                    {
                        //  Add more special processing for TableHeader and TableFooter
                        XElement xamlTableBodyElement = new XElement(nsp + Xaml_TableRowGroup);
                        xamlTableElement.Add(xamlTableBodyElement);

                        sourceContext.Add((XElement)htmlChildNode);

                        // Get properties of Html tbody element
                        Dictionary<object, object> tbodyElementLocalProperties;
                        Dictionary<object, object> tbodyElementCurrentProperties = GetElementProperties((XElement)htmlChildNode, currentProperties, out tbodyElementLocalProperties, sourceContext);
                        // TODO: apply local properties for tbody

                        // Process children of htmlChildNode, which is tbody, for tr elements
                        AddTableRowsToTableBody(xamlTableBodyElement, (htmlChildNode as XElement).FirstNode, tbodyElementCurrentProperties, columnStarts, sourceContext);
                        if (xamlTableBodyElement.Elements().Count() > 0)
                        {
                            xamlTableElement.Add(xamlTableBodyElement);
                            // else: if there is no TRs in this TBody, we simply ignore it
                        }

                        Debug.Assert(sourceContext.Count > 0 && sourceContext[sourceContext.Count - 1] == htmlChildNode);
                        sourceContext.RemoveAt(sourceContext.Count - 1);

                        htmlChildNode = htmlChildNode.NextNode;
                    }
                    else if (htmlChildName == "tr")
                    {
                        // Tbody is not present, but tr element is present. Tr is wrapped in tbody
                        XElement xamlTableBodyElement = new XElement(nsp + Xaml_TableRowGroup);

                        // We use currentProperties of xamlTableElement when adding rows since the tbody element is artificially created and has
                        // no properties of its own

                        htmlChildNode = AddTableRowsToTableBody(xamlTableBodyElement, htmlChildNode, currentProperties, columnStarts, sourceContext);
                        if (xamlTableBodyElement.Elements().Count() > 0)
                        {
                            xamlTableElement.Add(xamlTableBodyElement);
                        }
                    }
                    else
                    {
                        // Element is not tbody or tr. Ignore it.
                        // TODO: add processing for thead, tfoot elements and recovery for td elements
                        htmlChildNode = htmlChildNode.NextNode;
                    }
                }

                if (xamlTableElement.Elements().Count() > 0)
                {
                    xamlParentElement.Add(xamlTableElement);
                }
            }
        }

        private static XElement GetCellFromSingleCellTable(XElement htmlTableElement)
        {
            XElement singleCell = null;

            for (XNode tableChild = htmlTableElement.FirstNode; tableChild != null; tableChild = tableChild.NextNode)
            {
                string elementName = (tableChild as XElement == null) ? String.Empty : (tableChild as XElement).Name.LocalName.ToLower();
                if (elementName == "tbody" || elementName == "thead" || elementName == "tfoot")
                {
                    if (singleCell != null)
                    {
                        return null;
                    }
                    for (XNode tbodyChild = (tableChild as XElement).FirstNode; tbodyChild != null; tbodyChild = tbodyChild.NextNode)
                    {
                        if ((tbodyChild as XElement).Name.LocalName.ToLower() == "tr")
                        {
                            if (singleCell != null)
                            {
                                return null;
                            }
                            for (XNode trChild = tbodyChild.ElementsAfterSelf().First(); trChild != null; trChild = trChild.NextNode)
                            {
                                string cellName = (trChild as XElement).Name.LocalName.ToLower();
                                if (cellName == "td" || cellName == "th")
                                {
                                    if (singleCell != null)
                                    {
                                        return null;
                                    }
                                    singleCell = (XElement)trChild;
                                }
                            }
                        }
                    }
                }
                else if (elementName == "tr")
                {
                    if (singleCell != null)
                    {
                        return null;
                    }
                    for (XNode trChild = tableChild.NextNode; trChild != null; trChild = trChild.NextNode)
                    {
                        string cellName = (trChild as XElement == null) ? String.Empty : (trChild as XElement).Name.LocalName.ToLower();
                        if (cellName == "td" || cellName == "th")
                        {
                            if (singleCell != null)
                            {
                                return null;
                            }
                            singleCell = (XElement)trChild;
                        }
                    }
                }
            }

            return singleCell;
        }

        /// <summary>
        /// Processes the information about table columns - COLGROUP and COL html elements.
        /// </summary>
        /// <param name="htmlTableElement">
        /// XmlElement representing a source html table.
        /// </param>
        /// <param name="xamlTableElement">
        /// XmlElement repesenting a resulting xaml table.
        /// </param>
        /// <param name="columnStartsAllRows">
        /// Array of doubles - column start coordinates.
        /// Can be null, which means that column size information is not available
        /// and we must use source colgroup/col information.
        /// In case wneh it's not null, we will ignore source colgroup/col information.
        /// </param>
        /// <param name="currentProperties"></param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        private static void AddColumnInformation(XElement htmlTableElement, XElement xamlTableElement, List<object> columnStartsAllRows, Dictionary<object, object> currentProperties, List<XElement> sourceContext)
        {
            // Add column information
            if (columnStartsAllRows != null)
            {
                // We have consistent information derived from table cells; use it
                // The last element in columnStarts represents the end of the table
                for (int columnIndex = 0; columnIndex < columnStartsAllRows.Count - 1; columnIndex++)
                {
                    XElement xamlColumnElement;

                    XNamespace nsp = _xamlNamespace;
                    xamlColumnElement = new XElement(nsp + Xaml_TableColumn);
                    xamlColumnElement.Add(new XAttribute(Xaml_Width, ((double)columnStartsAllRows[columnIndex + 1] - (double)columnStartsAllRows[columnIndex]).ToString()));
                    xamlTableElement.Add(xamlColumnElement);
                }
            }
            else
            {
                // We do not have consistent information from table cells;
                // Translate blindly colgroups from html.
                for (XNode htmlChildNode = htmlTableElement.FirstNode; htmlChildNode != null; htmlChildNode = htmlChildNode.NextNode)
                {
                    if ((htmlChildNode as XElement).Name.LocalName.ToLower() == "colgroup")
                    {
                        // TODO: add column width information to this function as a parameter and process it
                        AddTableColumnGroup(xamlTableElement, (XElement)htmlChildNode, currentProperties, sourceContext);
                    }
                    else if ((htmlChildNode as XElement).Name.LocalName.ToLower() == "col")
                    {
                        AddTableColumn(xamlTableElement, (XElement)htmlChildNode, currentProperties, sourceContext);
                    }
                    else if (htmlChildNode is XElement)
                    {
                        // Some element which belongs to table body. Stop column loop.
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Converts htmlColgroupElement into Xaml TableColumnGroup element, and appends it to the parent
        /// xamlTableElement
        /// </summary>
        /// <param name="xamlTableElement">
        /// XmlElement representing Xaml Table element to which the converted column group should be added
        /// </param>
        /// <param name="htmlColgroupElement">
        /// XmlElement representing Html colgroup element to be converted
        /// <param name="inheritedProperties">
        /// Properties inherited from parent context
        /// </param>
        private static void AddTableColumnGroup(XElement xamlTableElement, XElement htmlColgroupElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            Dictionary<object, object> localProperties;
            Dictionary<object, object> currentProperties = GetElementProperties(htmlColgroupElement, inheritedProperties, out localProperties, sourceContext);

            // TODO: process local properties for colgroup

            // Process children of colgroup. Colgroup may contain only col elements.
            for (XNode htmlNode = htmlColgroupElement.FirstNode; htmlNode != null; htmlNode = htmlNode.NextNode)
            {
                if (htmlNode is XElement && (htmlNode as XElement).Name.LocalName.ToLower() == "col")
                {
                    AddTableColumn(xamlTableElement, (XElement)htmlNode, currentProperties, sourceContext);
                }
            }
        }

        /// <summary>
        /// Converts htmlColElement into Xaml TableColumn element, and appends it to the parent
        /// xamlTableColumnGroupElement
        /// </summary>
        /// <param name="xamlTableElement"></param>
        /// <param name="htmlColElement">
        /// XmlElement representing Html col element to be converted
        /// </param>
        /// <param name="inheritedProperties">
        /// properties inherited from parent context
        /// </param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        private static void AddTableColumn(XElement xamlTableElement, XElement htmlColElement, Dictionary<object, object> inheritedProperties, List<XElement> sourceContext)
        {
            Dictionary<object, object> localProperties;
            Dictionary<object, object> currentProperties = GetElementProperties(htmlColElement, inheritedProperties, out localProperties, sourceContext);

            XNamespace nsp = _xamlNamespace;
            XElement xamlTableColumnElement = new XElement(nsp + Xaml_TableColumn);

            // TODO: process local properties for TableColumn element

            // Col is an empty element, with no subtree
            xamlTableElement.Add(xamlTableColumnElement);
        }

        /// <summary>
        /// Adds TableRow elements to xamlTableBodyElement. The rows are converted from Html tr elements that
        /// may be the children of an Html tbody element or an Html table element with tbody missing
        /// </summary>
        /// <param name="xamlTableBodyElement">
        /// XmlElement representing Xaml TableRowGroup element to which the converted rows should be added
        /// </param>
        /// <param name="htmlTRStartNode">
        /// XmlElement representing the first tr child of the tbody element to be read
        /// </param>
        /// <param name="currentProperties">
        /// Hashtable representing current properties of the tbody element that are generated and applied in the
        /// AddTable function; to be used as inheritedProperties when adding tr elements
        /// </param>
        /// <param name="columnStarts"></param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        /// <returns>
        /// XmlNode representing the current position of the iterator among tr elements
        /// </returns>
        private static XNode AddTableRowsToTableBody(XElement xamlTableBodyElement, XNode htmlTRStartNode, Dictionary<object, object> currentProperties, List<object> columnStarts, List<XElement> sourceContext)
        {
            // Parameter validation
            Debug.Assert(xamlTableBodyElement.Name == Xaml_TableRowGroup);
            Debug.Assert(currentProperties != null);

            // Initialize child node for iteratimg through children to the first tr element
            XNode htmlChildNode = htmlTRStartNode;
            List<object> activeRowSpans = null;
            if (columnStarts != null)
            {
                activeRowSpans = new List<object>();
                InitializeActiveRowSpans(activeRowSpans, columnStarts.Count);
            }

            while (htmlChildNode != null && (htmlChildNode as XElement).Name.LocalName.ToLower() != "tbody")
            {
                if ((htmlChildNode as XElement).Name.LocalName.ToLower() == "tr")
                {
                    XNamespace nsp = _xamlNamespace;
                    XElement xamlTableRowElement = new XElement(nsp + Xaml_TableRow);

                    sourceContext.Add((XElement)htmlChildNode);

                    // Get tr element properties
                    Dictionary<object, object> trElementLocalProperties;
                    Dictionary<object, object> trElementCurrentProperties = GetElementProperties((XElement)htmlChildNode, currentProperties, out trElementLocalProperties, sourceContext);
                    // TODO: apply local properties to tr element

                    AddTableCellsToTableRow(xamlTableRowElement, htmlChildNode.ElementsAfterSelf().First(), trElementCurrentProperties, columnStarts, activeRowSpans, sourceContext);
                    if (xamlTableRowElement.Elements().Count() > 0)
                    {
                        xamlTableBodyElement.Add(xamlTableRowElement);
                    }

                    Debug.Assert(sourceContext.Count > 0 && sourceContext[sourceContext.Count - 1] == htmlChildNode);
                    sourceContext.RemoveAt(sourceContext.Count - 1);

                    // Advance
                    htmlChildNode = htmlChildNode.NextNode;

                }
                else if ((htmlChildNode as XElement).Name.LocalName.ToLower() == "td")
                {
                    // Tr element is not present. We create one and add td elements to it
                    XNamespace nsp = _xamlNamespace;
                    XElement xamlTableRowElement = new XElement(nsp + Xaml_TableRow);

                    // This is incorrect formatting and the column starts should not be set in this case
                    Debug.Assert(columnStarts == null);

                    htmlChildNode = AddTableCellsToTableRow(xamlTableRowElement, htmlChildNode, currentProperties, columnStarts, activeRowSpans, sourceContext);
                    if (xamlTableRowElement.Elements().Count() > 0)
                    {
                        xamlTableBodyElement.Add(xamlTableRowElement);
                    }
                }
                else
                {
                    // Not a tr or td  element. Ignore it.
                    // TODO: consider better recovery here
                    htmlChildNode = htmlChildNode.NextNode;
                }
            }
            return htmlChildNode;
        }

        /// <summary>
        /// Adds TableCell elements to xamlTableRowElement.
        /// </summary>
        /// <param name="xamlTableRowElement">
        /// XmlElement representing Xaml TableRow element to which the converted cells should be added
        /// </param>
        /// <param name="htmlTDStartNode">
        /// XmlElement representing the child of tr or tbody element from which we should start adding td elements
        /// </param>
        /// <param name="currentProperties">
        /// properties of the current html tr element to which cells are to be added
        /// </param>
        /// <returns>
        /// XmlElement representing the current position of the iterator among the children of the parent Html tbody/tr element
        /// </returns>
        private static XNode AddTableCellsToTableRow(XElement xamlTableRowElement, XNode htmlTDStartNode, Dictionary<object, object> currentProperties, List<object> columnStarts, List<object> activeRowSpans, List<XElement> sourceContext)
        {
            // parameter validation
            Debug.Assert(xamlTableRowElement.Name == Xaml_TableRow);
            Debug.Assert(currentProperties != null);
            if (columnStarts != null)
            {
                Debug.Assert(activeRowSpans.Count == columnStarts.Count);
            }

            XNode htmlChildNode = htmlTDStartNode;
            double columnStart = 0;
            double columnWidth = 0;
            int columnIndex = 0;
            int columnSpan = 0;

            while (htmlChildNode != null && (htmlChildNode as XElement).Name.LocalName.ToLower() != "tr" &&
                (htmlChildNode as XElement).Name.LocalName.ToLower() != "tbody" &&
                (htmlChildNode as XElement).Name.LocalName.ToLower() != "thead" &&
                (htmlChildNode as XElement).Name.LocalName.ToLower() != "tfoot")
            {
                if ((htmlChildNode as XElement).Name.LocalName.ToLower() == "td" ||
                    (htmlChildNode as XElement).Name.LocalName.ToLower() == "th")
                {
                    XNamespace nsp = _xamlNamespace;
                    XElement xamlTableCellElement = new XElement(nsp + Xaml_TableCell);

                    sourceContext.Add((XElement)htmlChildNode);

                    Dictionary<object, object> tdElementLocalProperties;
                    Dictionary<object, object> tdElementCurrentProperties = GetElementProperties((XElement)htmlChildNode, currentProperties, out tdElementLocalProperties, sourceContext);

                    // TODO: determine if localProperties can be used instead of htmlChildNode in this call, and if they can,
                    // make necessary changes and use them instead.
                    ApplyPropertiesToTableCellElement((XElement)htmlChildNode, xamlTableCellElement);

                    if (columnStarts != null)
                    {
                        Debug.Assert(columnIndex < columnStarts.Count - 1);
                        while (columnIndex < activeRowSpans.Count && (int)activeRowSpans[columnIndex] > 0)
                        {
                            activeRowSpans[columnIndex] = (int)activeRowSpans[columnIndex] - 1;
                            Debug.Assert((int)activeRowSpans[columnIndex] >= 0);
                            columnIndex++;
                        }
                        Debug.Assert(columnIndex < columnStarts.Count - 1);
                        columnStart = (double)columnStarts[columnIndex];
                        columnWidth = GetColumnWidth((XElement)htmlChildNode);
                        columnSpan = CalculateColumnSpan(columnIndex, columnWidth, columnStarts);
                        int rowSpan = GetRowSpan((XElement)htmlChildNode);

                        // Column cannot have no span
                        Debug.Assert(columnSpan > 0);
                        Debug.Assert(columnIndex + columnSpan < columnStarts.Count);

                        xamlTableCellElement.Add(new XAttribute(Xaml_TableCell_ColumnSpan, columnSpan.ToString()));

                        // Apply row span
                        for (int spannedColumnIndex = columnIndex; spannedColumnIndex < columnIndex + columnSpan; spannedColumnIndex++)
                        {
                            Debug.Assert(spannedColumnIndex < activeRowSpans.Count);
                            activeRowSpans[spannedColumnIndex] = (rowSpan - 1);
                            Debug.Assert((int)activeRowSpans[spannedColumnIndex] >= 0);
                        }

                        columnIndex = columnIndex + columnSpan;
                    }

                    AddDataToTableCell(xamlTableCellElement, htmlChildNode.ElementsAfterSelf().First(), tdElementCurrentProperties, sourceContext);
                    if (xamlTableCellElement.Elements().Count() > 0)
                    {
                        xamlTableRowElement.Add(xamlTableCellElement);
                    }

                    Debug.Assert(sourceContext.Count > 0 && sourceContext[sourceContext.Count - 1] == htmlChildNode);
                    sourceContext.RemoveAt(sourceContext.Count - 1);

                    htmlChildNode = htmlChildNode.NextNode;
                }
                else
                {
                    // Not td element. Ignore it.
                    // TODO: Consider better recovery
                    htmlChildNode = htmlChildNode.NextNode;
                }
            }
            return htmlChildNode;
        }

        /// <summary>
        /// adds table cell data to xamlTableCellElement
        /// </summary>
        /// <param name="xamlTableCellElement">
        /// XmlElement representing Xaml TableCell element to which the converted data should be added
        /// </param>
        /// <param name="htmlDataStartNode">
        /// XmlElement representing the start element of data to be added to xamlTableCellElement
        /// </param>
        /// <param name="currentProperties">
        /// Current properties for the html td/th element corresponding to xamlTableCellElement
        /// </param>
        private static void AddDataToTableCell(XElement xamlTableCellElement, XNode htmlDataStartNode, Dictionary<object, object> currentProperties, List<XElement> sourceContext)
        {
            // Parameter validation
            Debug.Assert(xamlTableCellElement.Name == Xaml_TableCell);
            Debug.Assert(currentProperties != null);

            for (XNode htmlChildNode = htmlDataStartNode; htmlChildNode != null; htmlChildNode = htmlChildNode != null ? htmlChildNode.NextNode : null)
            {
                // Process a new html element and add it to the td element
                htmlChildNode = AddBlock(xamlTableCellElement, htmlChildNode, currentProperties, sourceContext);
            }
        }

        /// <summary>
        /// Performs a parsing pass over a table to read information about column width and rowspan attributes. This information
        /// is used to determine the starting point of each column.
        /// </summary>
        /// <param name="htmlTableElement">
        /// XmlElement representing Html table whose structure is to be analyzed
        /// </param>
        /// <returns>
        /// ArrayList of type double which contains the function output. If analysis is successful, this ArrayList contains
        /// all the points which are the starting position of any column in the table, ordered from left to right.
        /// In case if analisys was impossible we return null.
        /// </returns>
        private static List<object> AnalyzeTableStructure(XElement htmlTableElement)
        {
            // Parameter validation
            Debug.Assert(htmlTableElement.Name.LocalName.ToLower() == "table");
            if (htmlTableElement.Elements().Count() == 0)
            {
                return null;
            }

            bool columnWidthsAvailable = true;

            List<object> columnStarts = new List<object>();
            List<object> activeRowSpans = new List<object>();
            Debug.Assert(columnStarts.Count == activeRowSpans.Count);

            XNode htmlChildNode = htmlTableElement.FirstNode;
            double tableWidth = 0;  // Keep track of table width which is the width of its widest row

            // Analyze tbody and tr elements
            while (htmlChildNode != null && columnWidthsAvailable)
            {
                Debug.Assert(columnStarts.Count == activeRowSpans.Count);

                string name = (htmlChildNode as XElement == null) ? String.Empty : (htmlChildNode as XElement).Name.LocalName.ToLower();
                switch (name)
                {
                    case "tbody":
                        // Tbody element, we should analyze its children for trows
                        double tbodyWidth = AnalyzeTbodyStructure((XElement)htmlChildNode, columnStarts, activeRowSpans, tableWidth);
                        if (tbodyWidth > tableWidth)
                        {
                            // Table width must be increased to supported newly added wide row
                            tableWidth = tbodyWidth;
                        }
                        else if (tbodyWidth == 0)
                        {
                            // Tbody analysis may return 0, probably due to unprocessable format.
                            // We should also fail.
                            columnWidthsAvailable = false; // interrupt the analisys
                        }
                        break;
                    case "tr":
                        // Table row. Analyze column structure within row directly
                        double trWidth = AnalyzeTRStructure((XElement)htmlChildNode, columnStarts, activeRowSpans, tableWidth);
                        if (trWidth > tableWidth)
                        {
                            tableWidth = trWidth;
                        }
                        else if (trWidth == 0)
                        {
                            columnWidthsAvailable = false; // interrupt the analisys
                        }
                        break;
                    case "td":
                        // Incorrect formatting, too deep to analyze at this level. Return null.
                        // TODO: implement analysis at this level, possibly by creating a new tr
                        columnWidthsAvailable = false; // interrupt the analisys
                        break;
                    default:
                        // Element should not occur directly in table. Ignore it.
                        break;
                }

                htmlChildNode = htmlChildNode.NextNode;
            }

            if (columnWidthsAvailable)
            {
                // Add an item for whole table width
                columnStarts.Add(tableWidth);
                VerifyColumnStartsAscendingOrder(columnStarts);
            }
            else
            {
                columnStarts = null;
            }

            return columnStarts;
        }

        /// <summary>
        /// Performs a parsing pass over a tbody to read information about column width and rowspan attributes. Information read about width
        /// attributes is stored in the reference ArrayList parameter columnStarts, which contains a list of all starting
        /// positions of all columns in the table, ordered from left to right. Row spans are taken into consideration when
        /// computing column starts
        /// </summary>
        /// <param name="htmlTbodyElement">
        /// XmlElement representing Html tbody whose structure is to be analyzed
        /// </param>
        /// <param name="columnStarts">
        /// ArrayList of type double which contains the function output. If analysis fails, this parameter is set to null
        /// </param>
        /// <param name="tableWidth">
        /// Current width of the table. This is used to determine if a new column when added to the end of table should
        /// come after the last column in the table or is actually splitting the last column in two. If it is only splitting
        /// the last column it should inherit row span for that column
        /// </param>
        /// <returns>
        /// Calculated width of a tbody.
        /// In case of non-analizable column width structure return 0;
        /// </returns>
        private static double AnalyzeTbodyStructure(XElement htmlTbodyElement, List<object> columnStarts, List<object> activeRowSpans, double tableWidth)
        {
            // Parameter validation
            Debug.Assert(htmlTbodyElement.Name.LocalName.ToLower() == "tbody");
            Debug.Assert(columnStarts != null);

            double tbodyWidth = 0;
            bool columnWidthsAvailable = true;

            if (htmlTbodyElement.Elements().Count() == 0)
            {
                return tbodyWidth;
            }

            // Set active row spans to 0 - thus ignoring row spans crossing tbody boundaries
            ClearActiveRowSpans(activeRowSpans);

            XNode htmlChildNode = htmlTbodyElement.FirstNode;

            // Analyze tr elements
            while (htmlChildNode != null && columnWidthsAvailable)
            {
                switch ((htmlChildNode as XElement).Name.LocalName.ToLower())
                {
                    case "tr":
                        double trWidth = AnalyzeTRStructure((XElement)htmlChildNode, columnStarts, activeRowSpans, tbodyWidth);
                        if (trWidth > tbodyWidth)
                        {
                            tbodyWidth = trWidth;
                        }
                        break;
                    case "td":
                        columnWidthsAvailable = false; // interrupt the analisys
                        break;
                    default:
                        break;
                }
                htmlChildNode = htmlChildNode.NextNode;
            }

            // Set active row spans to 0 - thus ignoring row spans crossing tbody boundaries
            ClearActiveRowSpans(activeRowSpans);

            return columnWidthsAvailable ? tbodyWidth : 0;
        }

        /// <summary>
        /// Performs a parsing pass over a tr element to read information about column width and rowspan attributes.
        /// </summary>
        /// <param name="htmlTRElement">
        /// XmlElement representing Html tr element whose structure is to be analyzed
        /// </param>
        /// <param name="columnStarts">
        /// ArrayList of type double which contains the function output. If analysis is successful, this ArrayList contains
        /// all the points which are the starting position of any column in the tr, ordered from left to right. If analysis fails,
        /// the ArrayList is set to null
        /// </param>
        /// <param name="activeRowSpans">
        /// ArrayList representing all columns currently spanned by an earlier row span attribute. These columns should
        /// not be used for data in this row. The ArrayList actually contains notation for all columns in the table, if the
        /// active row span is set to 0 that column is not presently spanned but if it is > 0 the column is presently spanned
        /// </param>
        /// <param name="tableWidth">
        /// Double value representing the current width of the table.
        /// Return 0 if analisys was insuccessful.
        /// </param>
        private static double AnalyzeTRStructure(XElement htmlTRElement, List<object> columnStarts, List<object> activeRowSpans, double tableWidth)
        {
            double columnWidth;

            // Parameter validation
            Debug.Assert(htmlTRElement.Name.LocalName.ToLower() == "tr");
            Debug.Assert(columnStarts != null);
            Debug.Assert(activeRowSpans != null);
            Debug.Assert(columnStarts.Count == activeRowSpans.Count);

            if (htmlTRElement.Elements().Count() == 0)
            {
                return 0;
            }

            bool columnWidthsAvailable = true;

            double columnStart = 0; // starting position of current column
            XNode htmlChildNode = htmlTRElement.FirstNode;
            int columnIndex = 0;
            double trWidth = 0;

            // Skip spanned columns to get to real column start
            if (columnIndex < activeRowSpans.Count)
            {
                Debug.Assert((double)columnStarts[columnIndex] >= columnStart);
                if ((double)columnStarts[columnIndex] == columnStart)
                {
                    // The new column may be in a spanned area
                    while (columnIndex < activeRowSpans.Count && (int)activeRowSpans[columnIndex] > 0)
                    {
                        activeRowSpans[columnIndex] = (int)activeRowSpans[columnIndex] - 1;
                        Debug.Assert((int)activeRowSpans[columnIndex] >= 0);
                        columnIndex++;
                        columnStart = (double)columnStarts[columnIndex];
                    }
                }
            }

            while (htmlChildNode != null && columnWidthsAvailable)
            {
                Debug.Assert(columnStarts.Count == activeRowSpans.Count);

                VerifyColumnStartsAscendingOrder(columnStarts);

                string name = (htmlChildNode as XElement == null) ? String.Empty : (htmlChildNode as XElement).Name.LocalName.ToLower();
                switch (name)
                {
                    case "td":
                        Debug.Assert(columnIndex <= columnStarts.Count);
                        if (columnIndex < columnStarts.Count)
                        {
                            Debug.Assert(columnStart <= (double)columnStarts[columnIndex]);
                            if (columnStart < (double)columnStarts[columnIndex])
                            {
                                columnStarts.Insert(columnIndex, columnStart);
                                // There can be no row spans now - the column data will appear here
                                // Row spans may appear only during the column analysis
                                activeRowSpans.Insert(columnIndex, 0);
                            }
                        }
                        else
                        {
                            // Column start is greater than all previous starts. Row span must still be 0 because
                            // we are either adding after another column of the same row, in which case it should not inherit
                            // the previous column's span. Otherwise we are adding after the last column of some previous
                            // row, and assuming the table widths line up, we should not be spanned by it. If there is
                            // an incorrect tbale structure where a columns starts in the middle of a row span, we do not
                            // guarantee correct output
                            columnStarts.Add(columnStart);
                            activeRowSpans.Add(0);
                        }
                        columnWidth = GetColumnWidth((XElement)htmlChildNode);
                        if (columnWidth != -1)
                        {
                            int nextColumnIndex;
                            int rowSpan = GetRowSpan((XElement)htmlChildNode);

                            nextColumnIndex = GetNextColumnIndex(columnIndex, columnWidth, columnStarts, activeRowSpans);
                            if (nextColumnIndex != -1)
                            {
                                // Entire column width can be processed without hitting conflicting row span. This means that
                                // column widths line up and we can process them
                                Debug.Assert(nextColumnIndex <= columnStarts.Count);

                                // Apply row span to affected columns
                                for (int spannedColumnIndex = columnIndex; spannedColumnIndex < nextColumnIndex; spannedColumnIndex++)
                                {
                                    activeRowSpans[spannedColumnIndex] = rowSpan - 1;
                                    Debug.Assert((int)activeRowSpans[spannedColumnIndex] >= 0);
                                }

                                columnIndex = nextColumnIndex;

                                // Calculate columnsStart for the next cell
                                columnStart = columnStart + columnWidth;

                                if (columnIndex < activeRowSpans.Count)
                                {
                                    Debug.Assert((double)columnStarts[columnIndex] >= columnStart);
                                    if ((double)columnStarts[columnIndex] == columnStart)
                                    {
                                        // The new column may be in a spanned area
                                        while (columnIndex < activeRowSpans.Count && (int)activeRowSpans[columnIndex] > 0)
                                        {
                                            activeRowSpans[columnIndex] = (int)activeRowSpans[columnIndex] - 1;
                                            Debug.Assert((int)activeRowSpans[columnIndex] >= 0);
                                            columnIndex++;
                                            columnStart = (double)columnStarts[columnIndex];
                                        }
                                    }
                                    // else: the new column does not start at the same time as a pre existing column
                                    // so we don't have to check it for active row spans, it starts in the middle
                                    // of another column which has been checked already by the GetNextColumnIndex function
                                }
                            }
                            else
                            {
                                // Full column width cannot be processed without a pre existing row span.
                                // We cannot analyze widths
                                columnWidthsAvailable = false;
                            }
                        }
                        else
                        {
                            // Incorrect column width, stop processing
                            columnWidthsAvailable = false;
                        }
                        break;
                    default:
                        break;
                }

                htmlChildNode = htmlChildNode.NextNode;
            }

            // The width of the tr element is the position at which it's last td element ends, which is calculated in
            // the columnStart value after each td element is processed
            if (columnWidthsAvailable)
            {
                trWidth = columnStart;
            }
            else
            {
                trWidth = 0;
            }

            return trWidth;
        }

        /// <summary>
        /// Gets row span attribute from htmlTDElement. Returns an integer representing the value of the rowspan attribute.
        /// Default value if attribute is not specified or if it is invalid is 1
        /// </summary>
        /// <param name="htmlTDElement">
        /// Html td element to be searched for rowspan attribute
        /// </param>
        private static int GetRowSpan(XElement htmlTDElement)
        {
            string rowSpanAsString;
            int rowSpan;

            rowSpanAsString = GetAttribute((XElement)htmlTDElement, "rowspan");
            if (rowSpanAsString != null)
            {
                if (!Int32.TryParse(rowSpanAsString, out rowSpan))
                {
                    // Ignore invalid value of rowspan; treat it as 1
                    rowSpan = 1;
                }
            }
            else
            {
                // No row span, default is 1
                rowSpan = 1;
            }
            return rowSpan;
        }

        /// <summary>
        /// Gets index at which a column should be inseerted into the columnStarts ArrayList. This is
        /// decided by the value columnStart. The columnStarts ArrayList is ordered in ascending order.
        /// Returns an integer representing the index at which the column should be inserted
        /// </summary>
        /// <param name="columnStarts">
        /// Array list representing starting coordinates of all columns in the table
        /// </param>
        /// <param name="columnStart">
        /// Starting coordinate of column we wish to insert into columnStart
        /// </param>
        /// <param name="columnIndex">
        /// Int representing the current column index. This acts as a clue while finding the insertion index.
        /// If the value of columnStarts at columnIndex is the same as columnStart, then this position alrady exists
        /// in the array and we can jsut return columnIndex.
        /// </param>
        /// <returns></returns>
        private static int GetNextColumnIndex(int columnIndex, double columnWidth, List<object> columnStarts, List<object> activeRowSpans)
        {
            double columnStart;
            int spannedColumnIndex;

            // Parameter validation
            Debug.Assert(columnStarts != null);
            Debug.Assert(0 <= columnIndex && columnIndex <= columnStarts.Count);
            Debug.Assert(columnWidth > 0);

            columnStart = (double)columnStarts[columnIndex];
            spannedColumnIndex = columnIndex + 1;

            while (spannedColumnIndex < columnStarts.Count && (double)columnStarts[spannedColumnIndex] < columnStart + columnWidth && spannedColumnIndex != -1)
            {
                if ((int)activeRowSpans[spannedColumnIndex] > 0)
                {
                    // The current column should span this area, but something else is already spanning it
                    // Not analyzable
                    spannedColumnIndex = -1;
                }
                else
                {
                    spannedColumnIndex++;
                }
            }

            return spannedColumnIndex;
        }


        /// <summary>
        /// Used for clearing activeRowSpans array in the beginning/end of each tbody
        /// </summary>
        /// <param name="activeRowSpans">
        /// ArrayList representing currently active row spans
        /// </param>
        private static void ClearActiveRowSpans(List<object> activeRowSpans)
        {
            for (int columnIndex = 0; columnIndex < activeRowSpans.Count; columnIndex++)
            {
                activeRowSpans[columnIndex] = 0;
            }
        }

        /// <summary>
        /// Used for initializing activeRowSpans array in the before adding rows to tbody element
        /// </summary>
        /// <param name="activeRowSpans">
        /// ArrayList representing currently active row spans
        /// </param>
        /// <param name="count">
        /// Size to be give to array list
        /// </param>
        private static void InitializeActiveRowSpans(List<object> activeRowSpans, int count)
        {
            for (int columnIndex = 0; columnIndex < count; columnIndex++)
            {
                activeRowSpans.Add(0);
            }
        }


        /// <summary>
        /// Calculates width of next TD element based on starting position of current element and it's width, which
        /// is calculated byt he function
        /// </summary>
        /// <param name="htmlTDElement">
        /// XmlElement representing Html td element whose width is to be read
        /// </param>
        /// <param name="columnStart">
        /// Starting position of current column
        /// </param>
        private static double GetNextColumnStart(XElement htmlTDElement, double columnStart)
        {
            double columnWidth;
            double nextColumnStart;

            // Parameter validation
            Debug.Assert(htmlTDElement.Name.LocalName.ToLower() == "td" || htmlTDElement.Name.LocalName.ToLower() == "th");
            Debug.Assert(columnStart >= 0);

            nextColumnStart = -1;  // -1 indicates inability to calculate columnStart width

            columnWidth = GetColumnWidth(htmlTDElement);

            if (columnWidth == -1)
            {
                nextColumnStart = -1;
            }
            else
            {
                nextColumnStart = columnStart + columnWidth;
            }

            return nextColumnStart;
        }


        private static double GetColumnWidth(XElement htmlTDElement)
        {
            string columnWidthAsString;
            double columnWidth;

            columnWidthAsString = null;
            columnWidth = -1;

            // Get string valkue for the width
            columnWidthAsString = GetAttribute(htmlTDElement, "width");
            if (columnWidthAsString == null)
            {
                columnWidthAsString = GetCssAttribute(GetAttribute(htmlTDElement, "style"), "width");
            }

            // We do not allow column width to be 0, if specified as 0 we will fail to record it
            if (!TryGetLengthValue(columnWidthAsString, out columnWidth) || columnWidth == 0)
            {
                columnWidth = -1;
            }
            return columnWidth;
        }

        /// <summary>
        /// Calculates column span based the column width and the widths of all other columns. Returns an integer representing
        /// the column span
        /// </summary>
        /// <param name="columnIndex">
        /// Index of the current column
        /// </param>
        /// <param name="columnWidth">
        /// Width of the current column
        /// </param>
        /// <param name="columnStarts">
        /// ArrayList repsenting starting coordinates of all columns
        /// </param>
        private static int CalculateColumnSpan(int columnIndex, double columnWidth, List<object> columnStarts)
        {
            // Current status of column width. Indicates the amount of width that has been scanned already
            double columnSpanningValue;
            int columnSpanningIndex;
            int columnSpan;
            double subColumnWidth; // Width of the smallest-grain columns in the table

            Debug.Assert(columnStarts != null);
            Debug.Assert(columnIndex < columnStarts.Count - 1);
            Debug.Assert((double)columnStarts[columnIndex] >= 0);
            Debug.Assert(columnWidth > 0);

            columnSpanningIndex = columnIndex;
            columnSpanningValue = 0;
            columnSpan = 0;
            subColumnWidth = 0;

            while (columnSpanningValue < columnWidth && columnSpanningIndex < columnStarts.Count - 1)
            {
                subColumnWidth = (double)columnStarts[columnSpanningIndex + 1] - (double)columnStarts[columnSpanningIndex];
                Debug.Assert(subColumnWidth > 0);
                columnSpanningValue += subColumnWidth;
                columnSpanningIndex++;
            }

            // Now, we have either covered the width we needed to cover or reached the end of the table, in which
            // case the column spans all the columns until the end
            columnSpan = columnSpanningIndex - columnIndex;
            Debug.Assert(columnSpan > 0);

            return columnSpan;
        }

        /// <summary>
        /// Verifies that values in columnStart, which represent starting coordinates of all columns, are arranged
        /// in ascending order
        /// </summary>
        /// <param name="columnStarts">
        /// ArrayList representing starting coordinates of all columns
        /// </param>
        private static void VerifyColumnStartsAscendingOrder(List<object> columnStarts)
        {
            Debug.Assert(columnStarts != null);

            double columnStart;

            columnStart = -0.01;

            for (int columnIndex = 0; columnIndex < columnStarts.Count; columnIndex++)
            {
                Debug.Assert(columnStart < (double)columnStarts[columnIndex]);
                columnStart = (double)columnStarts[columnIndex];
            }
        }

        // .............................................................
        //
        // Attributes and Properties
        //
        // .............................................................

        /// <summary>
        /// Analyzes local properties of Html element, converts them into Xaml equivalents, and applies them to xamlElement
        /// </summary>
        /// <param name="xamlElement">
        /// XmlElement representing Xaml element to which properties are to be applied
        /// </param>
        /// <param name="localProperties">
        /// Hashtable representing local properties of Html element that is converted into xamlElement
        /// </param>
        private static void ApplyLocalProperties(XElement xamlElement, Dictionary<object, object> localProperties, bool isBlock)
        {
            bool marginSet = false;
            string marginTop = "0";
            string marginBottom = "0";
            string marginLeft = "0";
            string marginRight = "0";

            bool paddingSet = false;
            string paddingTop = "0";
            string paddingBottom = "0";
            string paddingLeft = "0";
            string paddingRight = "0";

            string borderColor = null;

            bool borderThicknessSet = false;
            string borderThicknessTop = "0";
            string borderThicknessBottom = "0";
            string borderThicknessLeft = "0";
            string borderThicknessRight = "0";

            IDictionaryEnumerator propertyEnumerator = localProperties.GetEnumerator();
            while (propertyEnumerator.MoveNext())
            {
                switch ((string)propertyEnumerator.Key)
                {
                    case "font-family":
                        //  Convert from font-family value list into xaml FontFamily value
                        xamlElement.Add(new XAttribute(Xaml_FontFamily, (string)propertyEnumerator.Value));
                        break;
                    case "font-style":
                        xamlElement.Add(new XAttribute(Xaml_FontStyle, (string)propertyEnumerator.Value));
                        break;
                    case "font-variant":
                        //  Convert from font-variant into xaml property
                        break;
                    case "font-weight":
                        xamlElement.Add(new XAttribute(Xaml_FontWeight, (string)propertyEnumerator.Value));
                        break;
                    case "font-size":
                        //  Convert from css size into FontSize
                        xamlElement.Add(new XAttribute(Xaml_FontSize, (string)propertyEnumerator.Value));
                        break;
                    case "color":
                        xamlElement.Add(new XAttribute(Xaml_Foreground, (string)propertyEnumerator.Value));
                        //SetPropertyValue(xamlElement, TextElement.ForegroundProperty, (string)propertyEnumerator.Value);
                        break;
                    case "mouseovercolor":
                        xamlElement.Add(new XAttribute(Xaml_MouseOverForeground, (string)propertyEnumerator.Value));
                        break;
                    case "background-color":
                        xamlElement.Add(new XAttribute(Xaml_Background, (string)propertyEnumerator.Value));
                        //SetPropertyValue(xamlElement, TextElement.BackgroundProperty, (string)propertyEnumerator.Value);
                        break;
                    case "text-decoration-underline":
                        if (!isBlock)
                        {
                            if ((string)propertyEnumerator.Value == "true")
                            {
                                xamlElement.Add(new XAttribute(Xaml_TextDecorations, Xaml_TextDecorations_Underline));
                            }
                        }
                        break;
                    case "text-decoration-none":
                    case "text-decoration-overline":
                    case "text-decoration-line-through":
                    case "text-decoration-blink":
                        //  Convert from all other text-decorations values
                        if (!isBlock)
                        {
                        }
                        break;
                    case "text-transform":
                        //  Convert from text-transform into xaml property
                        break;

                    case "text-indent":
                        if (isBlock)
                        {
                            xamlElement.Add(new XAttribute(Xaml_TextIndent, (string)propertyEnumerator.Value));
                        }
                        break;

                    case "text-align":
                        if (isBlock)
                        {
                            xamlElement.Add(new XAttribute(Xaml_TextAlignment, (string)propertyEnumerator.Value));
                        }
                        break;

                    case "width":
                    case "height":
                        //  Decide what to do with width and height propeties
                        break;

                    case "margin-top":
                        marginSet = true;
                        marginTop = (string)propertyEnumerator.Value;
                        break;
                    case "margin-right":
                        marginSet = true;
                        marginRight = (string)propertyEnumerator.Value;
                        break;
                    case "margin-bottom":
                        marginSet = true;
                        marginBottom = (string)propertyEnumerator.Value;
                        break;
                    case "margin-left":
                        marginSet = true;
                        marginLeft = (string)propertyEnumerator.Value;
                        break;

                    case "padding-top":
                        paddingSet = true;
                        paddingTop = (string)propertyEnumerator.Value;
                        break;
                    case "padding-right":
                        paddingSet = true;
                        paddingRight = (string)propertyEnumerator.Value;
                        break;
                    case "padding-bottom":
                        paddingSet = true;
                        paddingBottom = (string)propertyEnumerator.Value;
                        break;
                    case "padding-left":
                        paddingSet = true;
                        paddingLeft = (string)propertyEnumerator.Value;
                        break;

                    // NOTE: css names for elementary border styles have side indications in the middle (top/bottom/left/right)
                    // In our internal notation we intentionally put them at the end - to unify processing in ParseCssRectangleProperty method
                    case "border-color-top":
                        borderColor = (string)propertyEnumerator.Value;
                        break;
                    case "border-color-right":
                        borderColor = (string)propertyEnumerator.Value;
                        break;
                    case "border-color-bottom":
                        borderColor = (string)propertyEnumerator.Value;
                        break;
                    case "border-color-left":
                        borderColor = (string)propertyEnumerator.Value;
                        break;
                    case "border-style-top":
                    case "border-style-right":
                    case "border-style-bottom":
                    case "border-style-left":
                        //  Implement conversion from border style
                        break;
                    case "border-width-top":
                        borderThicknessSet = true;
                        borderThicknessTop = (string)propertyEnumerator.Value;
                        break;
                    case "border-width-right":
                        borderThicknessSet = true;
                        borderThicknessRight = (string)propertyEnumerator.Value;
                        break;
                    case "border-width-bottom":
                        borderThicknessSet = true;
                        borderThicknessBottom = (string)propertyEnumerator.Value;
                        break;
                    case "border-width-left":
                        borderThicknessSet = true;
                        borderThicknessLeft = (string)propertyEnumerator.Value;
                        break;

                    case "list-style-type":
                        if (xamlElement.Name == Xaml_List)
                        {
                            string markerStyle;
                            switch (((string)propertyEnumerator.Value).ToLower())
                            {
                                case "disc":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_Disc;
                                    break;
                                case "circle":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_Circle;
                                    break;
                                case "none":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_None;
                                    break;
                                case "square":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_Square;
                                    break;
                                case "box":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_Box;
                                    break;
                                case "lower-latin":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_LowerLatin;
                                    break;
                                case "upper-latin":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_UpperLatin;
                                    break;
                                case "lower-roman":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_LowerRoman;
                                    break;
                                case "upper-roman":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_UpperRoman;
                                    break;
                                case "decimal":
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_Decimal;
                                    break;
                                default:
                                    markerStyle = HtmlToXamlConverter.Xaml_List_MarkerStyle_Disc;
                                    break;
                            }
                            xamlElement.Add(new XAttribute(HtmlToXamlConverter.Xaml_List_MarkerStyle, markerStyle));
                        }
                        break;

                    case "float":
                    case "clear":
                        if (isBlock)
                        {
                            //  Convert float and clear properties
                        }
                        break;

                    case "display":
                        break;
                }
            }

            if (isBlock)
            {
                if (marginSet)
                {
                    ComposeThicknessProperty(xamlElement, Xaml_Margin, marginLeft, marginRight, marginTop, marginBottom);
                }

                if (paddingSet)
                {
                    ComposeThicknessProperty(xamlElement, Xaml_Padding, paddingLeft, paddingRight, paddingTop, paddingBottom);
                }

                if (borderColor != null)
                {
                    //  We currently ignore possible difference in brush colors on different border sides. Use the last colored side mentioned
                    xamlElement.Add(new XAttribute(Xaml_BorderBrush, borderColor));
                }

                if (borderThicknessSet)
                {
                    ComposeThicknessProperty(xamlElement, Xaml_BorderThickness, borderThicknessLeft, borderThicknessRight, borderThicknessTop, borderThicknessBottom);
                }
            }
        }

        // Create syntactically optimized four-value Thickness
        private static void ComposeThicknessProperty(XElement xamlElement, string propertyName, string left, string right, string top, string bottom)
        {
            // Xaml syntax:
            // We have a reasonable interpreation for one value (all four edges), two values (horizontal, vertical),
            // and four values (left, top, right, bottom).
            //  switch (i) {
            //    case 1: return new Thickness(lengths[0]);
            //    case 2: return new Thickness(lengths[0], lengths[1], lengths[0], lengths[1]);
            //    case 4: return new Thickness(lengths[0], lengths[1], lengths[2], lengths[3]);
            //  }
            string thickness;

            // We do not accept negative margins
            if (left[0] == '0' || left[0] == '-') left = "0";
            if (right[0] == '0' || right[0] == '-') right = "0";
            if (top[0] == '0' || top[0] == '-') top = "0";
            if (bottom[0] == '0' || bottom[0] == '-') bottom = "0";

            if (left == right && top == bottom)
            {
                if (left == top)
                {
                    thickness = left;
                }
                else
                {
                    thickness = left + "," + top;
                }
            }
            else
            {
                thickness = left + "," + top + "," + right + "," + bottom;
            }

            //  Need safer processing for a thickness value
            xamlElement.Add(new XAttribute(propertyName, thickness));
        }

        /// <summary>
        /// Analyzes the tag of the htmlElement and infers its associated formatted properties.
        /// After that parses style attribute and adds all inline css styles.
        /// The resulting style attributes are collected in output parameter localProperties.
        /// </summary>
        /// <param name="htmlElement">
        /// </param>
        /// <param name="inheritedProperties">
        /// set of properties inherited from ancestor elements. Currently not used in the code. Reserved for the future development.
        /// </param>
        /// <param name="localProperties">
        /// returns all formatting properties defined by this element - implied by its tag, its attributes, or its css inline style
        /// </param>
        /// <param name="stylesheet"></param>
        /// <param name="sourceContext"></param>
        /// <returns>
        /// returns a combination of previous context with local set of properties.
        /// This value is not used in the current code - inntended for the future development.
        /// </returns>
        private static Dictionary<object, object> GetElementProperties(XElement htmlElement, Dictionary<object, object> inheritedProperties, out Dictionary<object, object> localProperties, List<XElement> sourceContext)
        {
            // Start with context formatting properties
            Dictionary<object, object> currentProperties = new Dictionary<object, object>();
            IDictionaryEnumerator propertyEnumerator = inheritedProperties.GetEnumerator();
            while (propertyEnumerator.MoveNext())
            {
                currentProperties[propertyEnumerator.Key] = propertyEnumerator.Value;
            }

            // Identify element name
            string elementName = htmlElement.Name.LocalName.ToLower();

            // update current formatting properties depending on element tag

            localProperties = new Dictionary<object, object>();
            switch (elementName)
            {
                // Character formatting
                case "i":
                case "italic":
                case "em":
                    localProperties["font-style"] = "italic";
                    break;
                case "idea":
                    localProperties["color"] = "Gray";
                    localProperties["font-weight"] = "bold";
                    break;
                case "b":
                case "bold":
                case "strong":
                case "dfn":
                    localProperties["font-weight"] = "bold";
                    break;
                case "u":
                case "underline":
                    localProperties["text-decoration-underline"] = "true";
                    break;
                case "font":
                    string attributeValue = GetAttribute(htmlElement, "face");
                    if (attributeValue != null)
                    {
                        localProperties["font-family"] = attributeValue;
                    }
                    attributeValue = GetAttribute(htmlElement, "size");
                    if (attributeValue != null)
                    {
                        double fontSize = double.Parse(attributeValue) * (12.0 / 3.0);
                        if (fontSize < 1.0)
                        {
                            fontSize = 1.0;
                        }
                        else if (fontSize > 1000.0)
                        {
                            fontSize = 1000.0;
                        }
                        localProperties["font-size"] = fontSize.ToString();
                    }
                    attributeValue = GetAttribute(htmlElement, "color");
                    if (attributeValue != null)
                    {
                        localProperties["color"] = attributeValue;
                    }
                    break;
                case "samp":
                    localProperties["font-family"] = "Courier New"; // code sample
                    localProperties["font-size"] = Xaml_FontSize_XXSmall;
                    localProperties["text-align"] = "Left";
                    break;
                case "sub":
                    break;
                case "sup":
                    break;

                // Hyperlinks
                case "a": // href, hreflang, urn, methods, rel, rev, title
                    //  Set default hyperlink properties
                    localProperties["color"] = "Blue";
                    localProperties["mouseovercolor"] = "Gray";
                    break;
                case "acronym":
                    break;

                // Paragraph formatting:
                case "p":
                    string classValue = GetAttribute(htmlElement, "class");
                    if (classValue != null && classValue == "bold")
                    {
                        localProperties["font-weight"] = "bold";
                    }
                    break;
                case "div":
                    //  Set default div properties
                    break;
                case "pre":
                    localProperties["font-family"] = "Courier New"; // renders text in a fixed-width font
                    localProperties["font-size"] = Xaml_FontSize_XXSmall;
                    localProperties["text-align"] = "Left";
                    break;
                case "blockquote":
                    localProperties["margin-left"] = "16";
                    break;

                case "h1":
                    localProperties["font-size"] = Xaml_FontSize_XXLarge;
                    break;
                case "h2":
                    localProperties["font-size"] = Xaml_FontSize_XLarge;
                    break;
                case "h3":
                    localProperties["font-size"] = Xaml_FontSize_Large;
                    break;
                case "h4":
                    localProperties["font-size"] = Xaml_FontSize_Medium;
                    break;
                case "h5":
                    localProperties["font-size"] = Xaml_FontSize_Small;
                    break;
                case "h6":
                    localProperties["font-size"] = Xaml_FontSize_XSmall;
                    break;
                // List properties
                case "ul":
                    localProperties["list-style-type"] = "disc";
                    break;
                case "ol":
                    localProperties["list-style-type"] = "decimal";
                    break;

                case "table":
                case "body":
                case "html":
                    break;
            }

            // Combine local properties with context to create new current properties
            propertyEnumerator = localProperties.GetEnumerator();
            while (propertyEnumerator.MoveNext())
            {
                currentProperties[propertyEnumerator.Key] = propertyEnumerator.Value;
            }

            return currentProperties;
        }

        /// <summary>
        /// Extracts a value of css attribute from css style definition.
        /// </summary>
        /// <param name="cssStyle">
        /// Source csll style definition
        /// </param>
        /// <param name="attributeName">
        /// A name of css attribute to extract
        /// </param>
        /// <returns>
        /// A string rrepresentation of an attribute value if found;
        /// null if there is no such attribute in a given string.
        /// </returns>
        private static string GetCssAttribute(string cssStyle, string attributeName)
        {
            //  This is poor man's attribute parsing. Replace it by real css parsing
            if (cssStyle != null)
            {
                string[] styleValues;

                attributeName = attributeName.ToLower();

                // Check for width specification in style string
                styleValues = cssStyle.Split(';');

                for (int styleValueIndex = 0; styleValueIndex < styleValues.Length; styleValueIndex++)
                {
                    string[] styleNameValue;

                    styleNameValue = styleValues[styleValueIndex].Split(':');
                    if (styleNameValue.Length == 2)
                    {
                        if (styleNameValue[0].Trim().ToLower() == attributeName)
                        {
                            return styleNameValue[1].Trim();
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Converts a length value from string representation to a double.
        /// </summary>
        /// <param name="lengthAsString">
        /// Source string value of a length.
        /// </param>
        /// <param name="length"></param>
        /// <returns></returns>
        private static bool TryGetLengthValue(string lengthAsString, out double length)
        {
            length = Double.NaN;

            if (lengthAsString != null)
            {
                lengthAsString = lengthAsString.Trim().ToLower();

                // We try to convert currentColumnWidthAsString into a double. This will eliminate widths of type "50%", etc.
                if (lengthAsString.EndsWith("pt"))
                {
                    lengthAsString = lengthAsString.Substring(0, lengthAsString.Length - 2);
                    if (Double.TryParse(lengthAsString, out length))
                    {
                        length = (length * 96.0) / 72.0; // convert from points to pixels
                    }
                    else
                    {
                        length = Double.NaN;
                    }
                }
                else if (lengthAsString.EndsWith("px"))
                {
                    lengthAsString = lengthAsString.Substring(0, lengthAsString.Length - 2);
                    if (!Double.TryParse(lengthAsString, out length))
                    {
                        length = Double.NaN;
                    }
                }
                else
                {
                    if (!Double.TryParse(lengthAsString, out length)) // Assuming pixels
                    {
                        length = Double.NaN;
                    }
                }
            }

            return !Double.IsNaN(length);
        }

        // .................................................................
        //
        // Pasring Color Attribute
        //
        // .................................................................

        private static string GetColorValue(string colorValue)
        {
            // TODO: Implement color conversion
            return colorValue;
        }

        /// <summary>
        /// Applies properties to xamlTableCellElement based on the html td element it is converted from.
        /// </summary>
        /// <param name="htmlChildNode">
        /// Html td/th element to be converted to xaml
        /// </param>
        /// <param name="xamlTableCellElement">
        /// XmlElement representing Xaml element for which properties are to be processed
        /// </param>
        /// <remarks>
        /// TODO: Use the processed properties for htmlChildNode instead of using the node itself
        /// </remarks>
        private static void ApplyPropertiesToTableCellElement(XElement htmlChildNode, XElement xamlTableCellElement)
        {
            // Parameter validation
            Debug.Assert((htmlChildNode as XElement).Name.LocalName.ToLower() == "td" ||
                (htmlChildNode as XElement).Name.LocalName.ToLower() == "th");
            Debug.Assert(xamlTableCellElement.Name == Xaml_TableCell);

            // set default border thickness for xamlTableCellElement to enable gridlines
            xamlTableCellElement.Add(new XAttribute(Xaml_TableCell_BorderThickness, "1,1,1,1"));
            xamlTableCellElement.Add(new XAttribute(Xaml_TableCell_BorderBrush, Xaml_Brushes_Black));
            string rowSpanString = GetAttribute((XElement)htmlChildNode, "rowspan");
            if (rowSpanString != null)
            {
                xamlTableCellElement.Add(new XAttribute(Xaml_TableCell_RowSpan, rowSpanString));
            }
        }

        #endregion Private Methods

        // ----------------------------------------------------------------
        //
        // Internal Constants
        //
        // ----------------------------------------------------------------

        // The constants reprtesent all Xaml names used in a conversion
        public const string Xaml_FlowDocument = "FlowDocument";

        public const string Xaml_Run = "Run";
        public const string Xaml_Span = "Span";
        public const string Xaml_Hyperlink = "Hyperlink";
        public const string Xaml_Hyperlink_NavigateUri = "NavigateUri";
        public const string Xaml_Hyperlink_TargetName = "TargetName";
        public const string Xaml_Hyperlink_Click = "Click";

        public const string Xaml_Section = "Section";

        public const string Xaml_List = "List";

        public const string Xaml_List_MarkerStyle = "MarkerStyle";
        public const string Xaml_List_MarkerStyle_None = "None";
        public const string Xaml_List_MarkerStyle_Decimal = "Decimal";
        public const string Xaml_List_MarkerStyle_Disc = "Disc";
        public const string Xaml_List_MarkerStyle_Circle = "Circle";
        public const string Xaml_List_MarkerStyle_Square = "Square";
        public const string Xaml_List_MarkerStyle_Box = "Box";
        public const string Xaml_List_MarkerStyle_LowerLatin = "LowerLatin";
        public const string Xaml_List_MarkerStyle_UpperLatin = "UpperLatin";
        public const string Xaml_List_MarkerStyle_LowerRoman = "LowerRoman";
        public const string Xaml_List_MarkerStyle_UpperRoman = "UpperRoman";

        public const string Xaml_ListItem = "ListItem";

        public const string Xaml_LineBreak = "LineBreak";

        public const string Xaml_Paragraph = "Paragraph";

        public const string Xaml_Margin = "Margin";
        public const string Xaml_Padding = "Padding";
        public const string Xaml_BorderBrush = "BorderBrush";
        public const string Xaml_BorderThickness = "BorderThickness";

        public const string Xaml_Table = "Table";

        public const string Xaml_TableColumn = "TableColumn";
        public const string Xaml_TableRowGroup = "TableRowGroup";
        public const string Xaml_TableRow = "TableRow";

        public const string Xaml_TableCell = "TableCell";
        public const string Xaml_TableCell_BorderThickness = "BorderThickness";
        public const string Xaml_TableCell_BorderBrush = "BorderBrush";

        public const string Xaml_TableCell_ColumnSpan = "ColumnSpan";
        public const string Xaml_TableCell_RowSpan = "RowSpan";

        public const string Xaml_Width = "Width";
        public const string Xaml_Brushes_Black = "Black";
        public const string Xaml_FontFamily = "FontFamily";

        public const string Xaml_FontSize = "FontSize";
        public const string Xaml_FontSize_XXLarge = "29"; // "XXLarge";
        public const string Xaml_FontSize_XLarge = "27"; // "XLarge";
        public const string Xaml_FontSize_Large = "25"; // "Large";
        public const string Xaml_FontSize_Medium = "23"; // "Medium";
        public const string Xaml_FontSize_Small = "19"; // "Small";
        public const string Xaml_FontSize_XSmall = "17"; // "XSmall";
        public const string Xaml_FontSize_XXSmall = "15"; // "XXSmall";

        public const string Xaml_FontWeight = "FontWeight";
        public const string Xaml_FontWeight_Bold = "Bold";

        public const string Xaml_FontStyle = "FontStyle";

        public const string Xaml_Foreground = "Foreground";
        public const string Xaml_MouseOverForeground = "MouseOverForeground";
        public const string Xaml_Background = "Background";
        public const string Xaml_TextDecorations = "TextDecorations";
        public const string Xaml_TextDecorations_Underline = "Underline";

        public const string Xaml_TextIndent = "TextIndent";
        public const string Xaml_TextAlignment = "TextAlignment";

        // ---------------------------------------------------------------------
        //
        // Private Fields
        //
        // ---------------------------------------------------------------------

        #region Private Fields

        static string _xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        #endregion Private Fields
    }
}
